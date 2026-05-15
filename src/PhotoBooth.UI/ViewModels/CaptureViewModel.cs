using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 7: Photo Capture with camera preview and countdown
/// </summary>
public partial class CaptureViewModel : ViewModelBase, IDisposable
{
    private readonly ICameraService _cameraService;
    private readonly string _photosDirectory;
    
    [ObservableProperty]
    private int _currentPhotoIndex = 0;

    [ObservableProperty]
    private int _totalPhotos = 8;

    [ObservableProperty]
    private int _countdown = 3;

    [ObservableProperty]
    private bool _isCountingDown;

    [ObservableProperty]
    private bool _isCameraReady;

    [ObservableProperty]
    private bool _isShootingInProgress;

    [ObservableProperty]
    private bool _isLayout6;

    [ObservableProperty]
    private bool _isLayout2 = true;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private Bitmap? _cameraPreview;

    [ObservableProperty]
    private ObservableCollection<string> _capturedPhotos = new();

    [ObservableProperty]
    private string _statusMessage = "Đang kết nối camera...";

    [ObservableProperty]
    private Bitmap? _backgroundImage;

    private bool _disposed;

    // Task 3.1: Frame drop fields
    private volatile bool _isRenderingFrame = false;
    private long _droppedFrameCount = 0;

    // Task 3.3: Reconnect fields
    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 5;
    private volatile bool _isReconnecting = false;
    private bool _eventSubscribed = false;
    private readonly object _reconnectLock = new();
    private CancellationTokenSource? _reconnectCts; // Finding 5: cancel pending reconnect on Dispose

    // Task 3.6: Shooting cancellation
    private CancellationTokenSource? _shootingCts;

    public CaptureViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        TotalPhotos = SessionService.CurrentSession.SelectedLayout?.CaptureCount ?? 8;
        
        LoadCaptureBackground();
        
        // Use session directory prepared early by BackgroundSelectionViewModel.GoNext()
        // This gives Google Drive Desktop a head start to detect and sync the folder.
        // Fallback: create directory here if PrepareSessionDirectory wasn't called
        if (string.IsNullOrEmpty(SessionService.CurrentSession.SessionDirectory))
        {
            SessionService.PrepareSessionDirectory();
        }
        _photosDirectory = SessionService.CurrentSession.SessionDirectory!;
        Directory.CreateDirectory(_photosDirectory); // Ensure it exists
        
        // Initialize camera
        _cameraService = new CameraService();
        InitializeCameraAsync();
    }

    private void LoadCaptureBackground()
    {
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        string imagePath;
        
        if (layoutId == "layout6")
        {
            imagePath = "avares://PhotoBooth.UI/Assets/backgrounds/back7_anh6.png";
            IsLayout6 = true;
            IsLayout2 = false;
        }
        else // layout2
        {
            imagePath = "avares://PhotoBooth.UI/Assets/backgrounds/back7_anh2.png";
            IsLayout6 = false;
            IsLayout2 = true;
        }

        try
        {
            var oldBg = BackgroundImage;
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(imagePath)));
            oldBg?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load capture background: {ex.Message}");
        }
    }

    // Task 3.3b: Refactored InitializeCameraAsync with event guard and reconnect support
    // Task 6.5: Wrapped in try-catch — async void must never let exceptions escape unobserved
    private async void InitializeCameraAsync()
    {
        try
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
                        _ = ScheduleReconnectAsync(); // fire-and-forget from background Task.Run
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
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] InitializeCameraAsync unhandled error: {ex.Message}");
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                StatusMessage = "❌ Lỗi khởi tạo camera";
            });
        }
    }

    // Task 3.1: Frame drop when UI is busy
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
            Bitmap bitmap;
            using (var stream = new MemoryStream(frameBytes))
            {
                bitmap = new Bitmap(stream);
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_disposed) { bitmap.Dispose(); return; }
                    var oldBitmap = CameraPreview;
                    CameraPreview = bitmap;
                    oldBitmap?.Dispose();
                }
                finally
                {
                    _isRenderingFrame = false;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Frame display error: {ex.Message}");
            _isRenderingFrame = false;
        }
    }

    // Task 3.3c: CameraError handler with exponential backoff
    private async void OnCameraError(object? sender, EventArgs e)
    {
        if (_disposed) return;
        lock (_reconnectLock)
        {
            if (_isReconnecting) return;
            _isReconnecting = true;
            _reconnectAttempts = 0;
        }
        
        // Cancel any active shooting sequence
        _shootingCts?.Cancel();
        
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            IsCameraReady = false;
            StatusMessage = "⚠️ Mất kết nối Camera. Đang thử lại...";
        });
        
        await ScheduleReconnectAsync(); // Finding 8: await Task instead of fire-and-forget async void
    }

    // Finding 8: Changed from async void to async Task to prevent unobserved exceptions crashing the process
    private async Task ScheduleReconnectAsync()
    {
        if (_disposed) return;
        
        // Finding 5: Create a new CTS for this reconnect cycle so Dispose() can cancel pending delays
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;
        
        try
        {
            // Finding 7: Use Interlocked.Increment for thread-safe counter
            var attempt = Interlocked.Increment(ref _reconnectAttempts);
            
            if (attempt > MaxReconnectAttempts)
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
            int delayMs = 3000 * (int)Math.Pow(2, attempt - 1);
            Console.WriteLine($"[CAMERA] Reconnect attempt {attempt}/{MaxReconnectAttempts} in {delayMs}ms");
            
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                StatusMessage = $"⚠️ Mất kết nối Camera. Thử lại lần {attempt}/{MaxReconnectAttempts} sau {delayMs / 1000}s...";
            });
            
            await Task.Delay(delayMs, ct); // Finding 5: cancellable delay
            
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
        catch (OperationCanceledException)
        {
            // Finding 5: Dispose() cancelled the reconnect — normal, don't log as error
            Console.WriteLine("[CAMERA] Reconnect cancelled (Dispose)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] ScheduleReconnect error: {ex.Message}");
        }
    }

    [ObservableProperty]
    private int _countdownDuration = 1; // 5 seconds countdown

    // Task 3.6: Shooting sequence with cancellation support
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

    private async Task CapturePhotoAsync()
    {
        try
        {
            StatusMessage = "📸 Chụp!";
            
            // Flash effect
            IsFlashing = true;
            
            // Capture photo
            var photoPath = _cameraService.CapturePhoto(_photosDirectory);
            
            // Keep flash visible briefly
            await Task.Delay(200, _shootingCts?.Token ?? CancellationToken.None);
            IsFlashing = false;
            
            _shootingCts?.Token.ThrowIfCancellationRequested();
            
            // Crop ảnh theo layout
            var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
            if (layoutId == "layout2")
            {
                // Layout 2 ảnh: crop về 518x720, dịch phải 50px
                ImageCropService.CropToSize(photoPath, 518, 720, offsetX: 50);
            }
            else if (layoutId == "layout6")
            {
                // Layout 6 ảnh: crop về 659x720, dịch phải 50px
                ImageCropService.CropToSize(photoPath, 659, 720, offsetX: 50);
            }
            
            CapturedPhotos.Add(photoPath);
            SessionService.AddCapturedPhoto(photoPath);
            
            CurrentPhotoIndex++;
            
            StatusMessage = $"Đã chụp {CurrentPhotoIndex}/{TotalPhotos}";

            if (CurrentPhotoIndex >= TotalPhotos)
            {
                StatusMessage = "Hoàn thành! Đang chuyển...";
                await Task.Delay(1000, _shootingCts?.Token ?? CancellationToken.None);
                
                // Stop camera before navigating
                _cameraService.StopPreview();
                NavigationService.NavigateTo<PhotoSelectionViewModel>();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi chụp ảnh: {ex.Message}";
            IsCountingDown = false;
        }
    }

    [RelayCommand]
    private void Skip()
    {
        _cameraService.StopPreview();
        NavigationService.NavigateTo<PhotoSelectionViewModel>();
    }

    // Task 3.4: Safe dispose with InvokeAsync
    public void Dispose()
    {
        _disposed = true;
        // Cancel shooting sequence nếu đang chạy
        _shootingCts?.Cancel();
        _shootingCts?.Dispose();
        _shootingCts = null;
        // Finding 5: Cancel any pending reconnect delay immediately
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        _cameraService.FrameReady -= OnFrameReady;
        _cameraService.CameraError -= OnCameraError;
        // Dispose camera off-thread to avoid blocking UI during StopPreview().Wait(2000)
        var cam = _cameraService;
        Task.Run(() => cam.Dispose());
        // Dispose bitmap trên UI thread, dùng InvokeAsync để đảm bảo hoàn tất
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            CameraPreview?.Dispose();
            CameraPreview = null;
            BackgroundImage?.Dispose();
            BackgroundImage = null;
        });
    }
}
