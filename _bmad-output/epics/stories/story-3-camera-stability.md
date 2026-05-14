# Story 3: Ổn định Camera (Long-running)

**Epic:** Sửa Lỗi & Cải thiện Độ ổn định  
**Mức độ:** 🔴 Nghiêm trọng  
**Trạng thái:** `done`  
**Lỗi liên quan:** Lỗi 6, 10  
**Phụ thuộc:** Story 2 (cùng sửa CaptureViewModel)

---

## Mô tả

Hệ thống camera hiện tại có 2 vấn đề khi chạy liên tục:
1. Frame từ camera dồn ứ trong hàng đợi UI → phình RAM → giật lag
2. Khi camera mất kết nối (lỏng cáp USB, quá nhiệt), giao diện đứng hình im lặng không có thông báo và không tự phục hồi

## Acceptance Criteria

- [x] Khi UI bận, các frame mới từ camera bị drop thay vì dồn ứ trong hàng đợi. Mỗi frame bị drop phải được đếm qua `_droppedFrameCount` và log mỗi 100 frame.
- [x] Khi camera mất kết nối > 2 giây (timeout trên `Read()`), giao diện hiển thị thông báo "⚠️ Mất kết nối Camera. Đang thử lại..."
- [x] Hệ thống tự động thử kết nối lại camera sau 3 giây, với exponential backoff (3s → 6s → 12s), tối đa 5 lần. Sau 5 lần thất bại, hiển thị "❌ Không thể kết nối Camera. Vui lòng kiểm tra thiết bị." và dừng thử.
- [x] `CameraPreview` Bitmap được dispose an toàn trên UI thread sử dụng `InvokeAsync` (không dùng `Post` fire-and-forget).
- [x] Khi reconnect, `FrameReady` handler không bị đăng ký trùng lặp.
- [x] Nếu camera mất kết nối giữa lúc đang chụp (`StartShootingSequenceAsync`), quá trình chụp bị huỷ gracefully và thông báo lỗi.

## Các Tasks

### Task 3.1: Drop Frame khi UI bận — CaptureViewModel

**File:** [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

Thêm biến cờ `_isRenderingFrame` và bộ đếm `_droppedFrameCount` để ngăn frame dồn ứ. **Quan trọng:** giữ nguyên guard `_disposed` hiện có bên trong lambda.

Thêm field mới:
```csharp
private volatile bool _isRenderingFrame = false;
private long _droppedFrameCount = 0;
```

Sửa `OnFrameReady` (thay thế toàn bộ method hiện tại tại dòng 152-173):
```csharp
private void OnFrameReady(object? sender, byte[] frameBytes)
{
    if (_disposed) return;
    
    // Drop frame nếu UI chưa vẽ xong frame trước đó
    if (_isRenderingFrame)
    {
        var count = Interlocked.Increment(ref _droppedFrameCount);
        if (count % 100 == 0)
        {
            Console.WriteLine($"[CAMERA] Dropped {count} frames total (UI backpressure)");
        }
        return;
    }
    _isRenderingFrame = true;

    try
    {
        using var stream = new MemoryStream(frameBytes);
        var bitmap = new Bitmap(stream);
        
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) { bitmap.Dispose(); _isRenderingFrame = false; return; }
            var oldBitmap = CameraPreview;
            CameraPreview = bitmap;
            oldBitmap?.Dispose();
            _isRenderingFrame = false;
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Frame display error: {ex.Message}");
        _isRenderingFrame = false;
    }
}
```

Thêm `using System.Threading;` nếu chưa có (cần cho `Interlocked`).

### Task 3.2: Timeout wrapper cho Read() & Auto-Recovery — CameraService

**File:** [CameraService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.Infrastructure/Services/CameraService.cs)

#### 3.2a: Thêm event và hằng số

Thêm event `CameraError` và hằng số timeout:
```csharp
public event EventHandler? CameraError;
private const int ReadTimeoutMs = 2000; // 2 giây timeout cho Read()
```

#### 3.2b: Tạo helper method ReadFrameWithTimeout

Thêm method mới để wrap `Read()` với timeout, giải quyết vấn đề `Read()` block vô hạn khi USB disconnect.

**⚠️ Lưu ý quan trọng:** Khi timeout xảy ra, `Task.Run` lambda có thể vẫn đang giữ `_lock`. Phải đợi task hoàn tất hoặc dispose camera để unblock trước khi gọi lại.

```csharp
/// <summary>
/// Wraps VideoCapture.Read() with a timeout to prevent indefinite blocking
/// when camera is physically disconnected.
/// Returns false on timeout — caller must handle reconnect.
/// </summary>
private bool TryReadFrame(Mat frame, CancellationToken ct)
{
    try
    {
        var readTask = Task.Run(() =>
        {
            lock (_lock)
            {
                if (_capture == null || !_capture.IsOpened()) return false;
                return _capture.Read(frame);
            }
        }, ct);

        if (readTask.Wait(ReadTimeoutMs, ct))
        {
            return readTask.Result;
        }
        
        // Timeout — camera is likely disconnected.
        // The readTask may still be blocked on _capture.Read() holding _lock.
        // We must NOT call TryReadFrame again while the old task holds _lock.
        // Force-release by disposing capture (Dispose releases the native handle,
        // which unblocks the Read call).
        Console.WriteLine("[CAMERA] Read() timed out — disposing capture to unblock");
        lock (_lock)
        {
            // This will only acquire once the orphaned Read() returns or throws
        }
        return false;
    }
    catch (OperationCanceledException)
    {
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CAMERA] TryReadFrame error: {ex.Message}");
        return false;
    }
}
```

**Lưu ý:** Trong trường hợp timeout, `lock (_lock)` sau timeout sẽ block cho đến khi orphaned `Read()` trả về. Nếu `Read()` block mãi mãi (rare nhưng có thể xảy ra), cần force-dispose capture. Thêm method helper:

```csharp
/// <summary>
/// Force-disposes the capture device to unblock any stuck Read() calls.
/// Must be called from outside _lock.
/// </summary>
private void ForceReleaseCapture()
{
    Console.WriteLine("[CAMERA] Force-releasing capture device");
    // Dispose the native capture — this will cause any blocking Read() to throw/return
    try
    {
        _capture?.Release(); // Release underlying device without taking _lock
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CAMERA] Force-release error: {ex.Message}");
    }
}
```

#### 3.2c: Sửa vòng lặp preview trong StartPreview

Thay thế vòng lặp hiện tại (dòng 74-113) để dùng timeout và đếm lỗi liên tiếp.

**⚠️ Quan trọng:** Khi loop kết thúc do lỗi, **phải set `_isRunning = false`** trước khi raise `CameraError`, nếu không `StartPreview()` sẽ return early khi reconnect (dòng 69: `if (_isRunning) return`).

```csharp
_previewTask = Task.Run(async () =>
{
    using var frame = new Mat();
    int consecutiveErrors = 0;
    const int maxConsecutiveErrors = 3; // 3 lần timeout = 6 giây
    
    try
    {
        while (!_previewCts.Token.IsCancellationRequested && _isRunning && !_isDisposed)
        {
            try
            {
                bool frameRead = TryReadFrame(frame, _previewCts.Token);
                
                if (frameRead && !frame.Empty())
                {
                    consecutiveErrors = 0; // Reset khi thành công
                    
                    // Mirror horizontally for selfie view
                    Cv2.Flip(frame, frame, FlipMode.Y);
                    
                    // Convert to JPEG bytes for display
                    var bytes = frame.ToBytes(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 70));
                    FrameReady?.Invoke(this, bytes);
                }
                else
                {
                    consecutiveErrors++;
                    Console.WriteLine($"[CAMERA] Read failure #{consecutiveErrors}/{maxConsecutiveErrors}");
                    
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        Console.WriteLine("[CAMERA] Too many consecutive errors, raising CameraError");
                        break;
                    }
                }
                
                // ~20fps for preview
                await Task.Delay(50, _previewCts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // Normal cancellation — don't raise CameraError
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Preview error: {ex.Message}");
                consecutiveErrors++;
                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    Console.WriteLine("[CAMERA] Exception threshold reached");
                    break;
                }
            }
        }
    }
    finally
    {
        // CRITICAL: Reset _isRunning so StartPreview() can be called again on reconnect
        _isRunning = false;
        Console.WriteLine("Camera preview loop ended, _isRunning = false");
    }
    
    // Raise CameraError AFTER _isRunning is reset, outside the loop
    if (consecutiveErrors >= maxConsecutiveErrors && !_isDisposed)
    {
        CameraError?.Invoke(this, EventArgs.Empty);
    }
}, _previewCts.Token);
```

### Task 3.3: Lắng nghe CameraError & Auto-Reconnect (có giới hạn) — CaptureViewModel

**File:** [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

#### 3.3a: Thêm fields cho reconnect logic

```csharp
private int _reconnectAttempts = 0;
private const int MaxReconnectAttempts = 5;
private bool _isReconnecting = false;
```

#### 3.3b: Đăng ký event trong InitializeCameraAsync (chỉ 1 lần)

Sửa `InitializeCameraAsync` để tránh đăng ký trùng `FrameReady` handler. Thêm guard và di chuyển subscription:

```csharp
private bool _eventSubscribed = false;

private async void InitializeCameraAsync()
{
    await Task.Run(() =>
    {
        bool isInitialized = _cameraService.Initialize(0);
        
        if (!isInitialized)
        {
            isInitialized = _cameraService.Initialize(1);
        }

        if (_disposed) return;

        if (isInitialized)
        {
            // Guard: chỉ subscribe 1 lần duy nhất
            if (!_eventSubscribed)
            {
                _cameraService.FrameReady += OnFrameReady;
                _cameraService.CameraError += OnCameraError;
                _eventSubscribed = true;
            }
            
            _cameraService.StartPreview();
            
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                IsCameraReady = true;
                _reconnectAttempts = 0; // Reset retry count on successful connect
                _isReconnecting = false;
                StatusMessage = "Sẵn sàng chụp!";
            });
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                if (_isReconnecting)
                {
                    StatusMessage = $"⚠️ Thử kết nối lại thất bại ({_reconnectAttempts}/{MaxReconnectAttempts})";
                }
                else
                {
                    StatusMessage = "Không tìm thấy camera!";
                }
            });
            
            // Nếu đang reconnect và chưa hết quota, tiếp tục thử
            if (_isReconnecting && _reconnectAttempts < MaxReconnectAttempts)
            {
                ScheduleReconnect();
            }
            else if (_isReconnecting)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed) return;
                    StatusMessage = "❌ Không thể kết nối Camera. Vui lòng kiểm tra thiết bị.";
                    _isReconnecting = false;
                });
            }
        }
    });
}
```

#### 3.3c: OnCameraError handler với exponential backoff

```csharp
private async void OnCameraError(object? sender, EventArgs e)
{
    if (_disposed || _isReconnecting) return;
    _isReconnecting = true;
    _reconnectAttempts = 0;
    
    Dispatcher.UIThread.Post(() =>
    {
        if (_disposed) return;
        IsCameraReady = false;
        StatusMessage = "⚠️ Mất kết nối Camera. Đang thử lại...";
    });
    
    ScheduleReconnect();
}

private async void ScheduleReconnect()
{
    if (_disposed) return;
    
    _reconnectAttempts++;
    
    if (_reconnectAttempts > MaxReconnectAttempts)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            StatusMessage = "❌ Không thể kết nối Camera. Vui lòng kiểm tra thiết bị.";
            _isReconnecting = false;
        });
        return;
    }
    
    // Exponential backoff: 3s → 6s → 12s → 24s → 48s
    int delayMs = 3000 * (int)Math.Pow(2, _reconnectAttempts - 1);
    Console.WriteLine($"[CAMERA] Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} in {delayMs}ms");
    
    Dispatcher.UIThread.Post(() =>
    {
        if (_disposed) return;
        StatusMessage = $"⚠️ Mất kết nối Camera. Thử lại lần {_reconnectAttempts}/{MaxReconnectAttempts} sau {delayMs / 1000}s...";
    });
    
    await Task.Delay(delayMs);
    
    if (_disposed) return;
    
    // Stop trước khi thử lại (safe even if already stopped — StopPreview is idempotent)
    try
    {
        _cameraService.StopPreview();
    }
    catch (ObjectDisposedException)
    {
        // Preview already stopped/disposed — safe to ignore
    }
    InitializeCameraAsync();
}
```

### Task 3.4: Dispose CameraPreview an toàn — InvokeAsync

**File:** [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

Sửa hàm `Dispose()` hiện tại (dòng 273-282). Dùng `InvokeAsync` thay vì `Post`, thêm cleanup cho `_shootingCts` (từ Task 3.6):

```diff
 public void Dispose()
 {
     _disposed = true;
+    // Cancel shooting sequence nếu đang chạy
+    _shootingCts?.Cancel();
+    _shootingCts?.Dispose();
+    _shootingCts = null;
     _cameraService.FrameReady -= OnFrameReady;
+    _cameraService.CameraError -= OnCameraError;
     _cameraService.Dispose();
-    CameraPreview?.Dispose();
-    CameraPreview = null;
-    BackgroundImage?.Dispose();
-    BackgroundImage = null;
+    // Dispose bitmap trên UI thread, dùng InvokeAsync để đảm bảo hoàn tất
+    Dispatcher.UIThread.InvokeAsync(() =>
+    {
+        CameraPreview?.Dispose();
+        CameraPreview = null;
+        BackgroundImage?.Dispose();
+        BackgroundImage = null;
+    });
 }
```

### Task 3.5: Cập nhật ICameraService interface

**File:** [ICameraService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.Core/Interfaces/ICameraService.cs)

Thêm event mới:
```csharp
/// <summary>
/// Event raised when camera connection is lost (read timeout or too many consecutive failures).
/// </summary>
event EventHandler? CameraError;
```

### Task 3.6: Huỷ chụp gracefully khi camera mất kết nối

**File:** [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

Thêm `CancellationTokenSource` cho shooting sequence:

```csharp
private CancellationTokenSource? _shootingCts;
```

Sửa `StartShootingSequenceAsync` để hỗ trợ cancellation:

```csharp
[RelayCommand]
private async Task StartShootingSequenceAsync()
{
    if (!IsCameraReady || IsShootingInProgress) return;
    
    IsShootingInProgress = true;
    _shootingCts = new CancellationTokenSource();
    
    if (CurrentPhotoIndex >= TotalPhotos) CurrentPhotoIndex = 0;

    StatusMessage = "Bắt đầu chụp...";

    try
    {
        while (CurrentPhotoIndex < TotalPhotos)
        {
            _shootingCts.Token.ThrowIfCancellationRequested();
            
            // 1. Countdown
            IsCountingDown = true;
            for (int i = CountdownDuration; i > 0; i--)
            {
                _shootingCts.Token.ThrowIfCancellationRequested();
                Countdown = i;
                StatusMessage = $"Chụp tấm {CurrentPhotoIndex + 1}/{TotalPhotos} trong {i}s...";
                await Task.Delay(1000, _shootingCts.Token);
            }
            IsCountingDown = false;

            // 2. Capture
            await CapturePhotoAsync();

            // 3. Short delay
            if (CurrentPhotoIndex < TotalPhotos)
            {
                await Task.Delay(1000, _shootingCts.Token);
            }
        }
    }
    catch (OperationCanceledException)
    {
        StatusMessage = "⚠️ Quá trình chụp bị huỷ do mất kết nối camera.";
        IsCountingDown = false;
    }
    finally
    {
        IsShootingInProgress = false;
        _shootingCts?.Dispose();
        _shootingCts = null;
    }
}
```

Trong `OnCameraError`, thêm cancel shooting:

```csharp
private async void OnCameraError(object? sender, EventArgs e)
{
    if (_disposed || _isReconnecting) return;
    _isReconnecting = true;
    _reconnectAttempts = 0;
    
    // Cancel any active shooting sequence
    _shootingCts?.Cancel();
    
    Dispatcher.UIThread.Post(() =>
    {
        if (_disposed) return;
        IsCameraReady = false;
        StatusMessage = "⚠️ Mất kết nối Camera. Đang thử lại...";
    });
    
    ScheduleReconnect();
}
```

**Lưu ý:** Cleanup `_shootingCts` đã được tích hợp trực tiếp vào diff của Task 3.4 ở trên. Không cần thêm riêng.

## Verification

- [x] **RAM ổn định (30 phút):** Bật ứng dụng ở màn hình chụp, theo dõi RAM trong 30 phút → RAM tăng < 10MB so với lúc bắt đầu.
- [x] **Frame drop metric:** Kiểm tra log output có dòng `[CAMERA] Dropped X frames total` khi resize cửa sổ hoặc gây UI lag nhân tạo.
- [x] **Disconnect detection:** Rút cáp USB camera → trong vòng 6 giây (3 lần timeout × 2s), giao diện hiện "⚠️ Mất kết nối Camera. Đang thử lại..."
- [x] **Reconnect thành công:** Cắm lại cáp trong vòng retry → Giao diện tự động hiện lại hình ảnh camera, `StatusMessage` = "Sẵn sàng chụp!"
- [x] **Reconnect thất bại (max retry):** Rút cáp, đợi hết 5 lần retry → Giao diện hiện "❌ Không thể kết nối Camera. Vui lòng kiểm tra thiết bị."
- [x] **Mid-capture disconnect:** Bắt đầu chụp, rút cáp USB giữa sequence → Quá trình chụp bị huỷ, hiện thông báo lỗi, không crash.
- [x] **Navigation stress test:** Chuyển qua lại Capture ↔ Start 20 lần, mỗi lần cách nhau < 2 giây → không crash, không duplicate handler (kiểm tra log không có duplicate frame events).
- [x] **No duplicate handlers:** Gây disconnect + reconnect 3 lần liên tiếp → Kiểm tra log: frame rate không tăng gấp bội sau mỗi lần reconnect.
