# Story 3: Detect Offline & Hiện thông báo liên hệ nhân viên

**Epic:** Offline Fallback — Mã số ảnh & Xử lý mất mạng  
**Mức độ:** 🟡 Nhẹ  
**Trạng thái:** `done`  
**Phụ thuộc:** Story 1 (Sequential Number Service)

---

## Mô tả

Khi app phát hiện mất mạng (không gọi được Apps Script), thay vì hiện QR trống/lỗi → hiển thị **thông báo "Hãy liên hệ nhân viên hỗ trợ"** kèm **mã số ảnh** để nhân viên (chủ booth) tìm đúng ảnh cho khách. Chủ booth sẽ trao đổi thông tin liên hệ trực tiếp với khách tại chỗ.

## Acceptance Criteria

- [x] Tạo `NetworkCheckService` — kiểm tra kết nối internet (A1: sử dụng shared HttpClient, cache 10s)
- [x] `QRCodeViewModel` gọi `NetworkCheckService.IsOnlineAsync()` — nếu false → set `IsOffline=true`, hiển thị SequentialNumber ngay lập tức
- [x] `ThankYouViewModel` gọi `NetworkCheckService.IsOnlineAsync()` tương tự — set `IsOffline=true` khi offline
- [x] Khi offline: hiện thông báo "Hãy liên hệ nhân viên hỗ trợ" + mã số ảnh (hoặc "---" nếu không có)
- [x] Khi online: QR hoạt động bình thường (không thay đổi logic hiện tại)
- [x] Catch block trong cả 2 ViewModel cũng activate offline mode khi API call fails (D4)
- [x] ThankYouView ẩn "Next" button khi offline, dùng đúng command name (D3, Q3)
- [x] Build thành công, không lỗi

## Các Tasks

### Task 3.1: Tạo NetworkCheckService

#### Tạo file mới: `src/PhotoBooth.UI/Services/NetworkCheckService.cs`

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Kiểm tra kết nối internet bằng cách ping Google's generate_204 endpoint.
/// Kết quả cache 10 giây để tránh gọi liên tục.
/// 
/// A1: Uses shared HttpService.Client — no socket exhaustion risk.
/// A2: Benign data race on _lastResult/_lastCheck is acceptable on x64.
/// </summary>
public static class NetworkCheckService
{
    private static bool _lastResult = true;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    public static async Task<bool> IsOnlineAsync()
    {
        if ((DateTime.UtcNow - _lastCheck) < CacheDuration)
            return _lastResult;

        try
        {
            // A1: Use shared HttpClient — avoids socket exhaustion
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await HttpService.Client.GetAsync("https://www.google.com/generate_204", cts.Token);
            _lastResult = response.IsSuccessStatusCode;
        }
        catch
        {
            _lastResult = false;
        }

        _lastCheck = DateTime.UtcNow;
        Console.WriteLine($"[NETWORK] Online check: {_lastResult}");
        return _lastResult;
    }

    internal static void ResetCache()
    {
        _lastCheck = DateTime.MinValue;
        _lastResult = true;
    }
}
```

### Task 3.2: Thêm properties cho offline UI vào QRCodeViewModel

#### [QRCodeViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/QRCodeViewModel.cs)

Thêm properties:

```csharp
/// True when the device is offline — shows offline fallback UI.
[ObservableProperty]
private bool _isOffline;

/// D2: Shows sequential number or fallback "---" when unavailable.
[ObservableProperty]
private string _sequentialNumber = "";
```

### Task 3.3: Sửa logic `UploadAndGenerateQR()` trong QRCodeViewModel

#### [QRCodeViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/QRCodeViewModel.cs)

Thêm check online ở đầu method `UploadAndGenerateQR()`:

```csharp
// D2: Get sequential number early — fallback to "---" if unavailable
SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";

// Check mạng trước
var isOnline = await NetworkCheckService.IsOnlineAsync();

if (!isOnline)
{
    // OFFLINE MODE: hiện thông báo liên hệ nhân viên
    Console.WriteLine($"[QR] Offline mode — seq: {SequentialNumber}");
    IsOffline = true;
    IsUploading = false;
    StatusText = "📴 Không có kết nối mạng";
    return;
}
```

Trong `catch (Exception ex)`, cũng chuyển sang offline mode (D4):

```csharp
catch (Exception ex)
{
    Console.WriteLine($"[QR] Error: {ex.GetType().Name}");
    SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";
    IsOffline = true;
    StatusText = "📴 Lỗi kết nối mạng";
    IsUploading = false;
}
```

### Task 3.4: Tương tự cho ThankYouViewModel

#### [ThankYouViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs)

Thêm properties `IsOffline`, `SequentialNumber` và check online tương tự Task 3.2 & 3.3.
Catch block cũng activates offline mode khi API call fails.

### Task 3.5: Cập nhật QRCodeView.axaml — thêm offline fallback UI

#### [QRCodeView.axaml](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Views/QRCodeView.axaml)

**Ẩn QR khi offline:** Thêm `IsVisible="{Binding !IsOffline}"` vào StackPanel chứa QR hiện tại.

**Thêm panel offline mới:**

```xml
<!-- Offline Fallback UI -->
<StackPanel IsVisible="{Binding IsOffline}"
            VerticalAlignment="Center" HorizontalAlignment="Center" 
            Spacing="25">
    
    <TextBlock Text="📴 Mất kết nối mạng" 
               FontSize="36" FontWeight="Bold" 
               Foreground="#FFA500"
               HorizontalAlignment="Center"/>
    
    <!-- Mã số ảnh nổi bật -->
    <Border Background="#0f3460" CornerRadius="15" Padding="30,20"
            HorizontalAlignment="Center">
        <StackPanel Spacing="8">
            <TextBlock Text="MÃ SỐ ẢNH CỦA BẠN" 
                       FontSize="18" Foreground="#888"
                       HorizontalAlignment="Center"/>
            <TextBlock Text="{Binding SequentialNumber}" 
                       FontSize="64" FontWeight="Bold" 
                       Foreground="#00d4ff"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Border>
    
    <!-- Thông báo liên hệ nhân viên -->
    <Border Background="#1e1e3a" CornerRadius="15" Padding="30,20"
            HorizontalAlignment="Center">
        <TextBlock Text="Vui lòng liên hệ nhân viên hỗ trợ để nhận ảnh" 
                   FontSize="24" FontWeight="SemiBold"
                   Foreground="White"
                   HorizontalAlignment="Center"
                   TextWrapping="Wrap"
                   TextAlignment="Center"/>
    </Border>
    
    <TextBlock Text="Ảnh đã được lưu và sẽ tự đồng bộ khi có mạng ✅" 
               FontSize="16" Foreground="#666"
               HorizontalAlignment="Center"/>
    
    <Button Command="{Binding ShowReturnCommand}"
            Content="🏠 Về màn hình chính"
            Width="350" Height="70"
            Background="#e94560"
            Foreground="White"
            FontSize="22"
            FontWeight="Bold"
            CornerRadius="15"
            HorizontalContentAlignment="Center"/>
</StackPanel>
```

### Task 3.6: Cập nhật ThankYouView.axaml — thêm offline fallback UI

#### [ThankYouView.axaml](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Views/ThankYouView.axaml)

P1: ThankYouView update IS mandatory (removed "nếu cần" qualifier).

**Changes:**
- Online QR content wrapped with `IsVisible="{Binding !IsOffline}"`
- D3: "Next" button hidden when offline
- Q3: Offline panel uses `GoToThankYouCommand` (correct command for ThankYou VM's 3-state flow)
- Same offline UI design as QRCodeView

---

## Lưu ý thiết kế

### Q2: Cache Behavior Note
NetworkCheckService caches kết quả 10 giây. Nếu mạng mất giữa session, sẽ có tối đa 10 giây delay trước khi detect offline. Điều này chấp nhận được cho kiosk app.

### UI khi ONLINE (giữ nguyên):
```
┌──────────────────────────┐
│   ✓ IN ẢNH THÀNH CÔNG!  │
│      ┌──────────┐        │
│      │ QR CODE  │        │
│      └──────────┘        │
│   📱 Quét mã QR tải ảnh │
│   🏠 Về màn hình chính  │
└──────────────────────────┘
```

### UI khi OFFLINE (mới):
```
┌──────────────────────────┐
│   📴 Mất kết nối mạng   │
│                          │
│   ┌──────────────────┐   │
│   │  MÃ SỐ ẢNH      │   │
│   │    0516-001      │   │
│   └──────────────────┘   │
│                          │
│   Vui lòng liên hệ      │
│   nhân viên hỗ trợ      │
│   để nhận ảnh            │
│                          │
│   Ảnh sẽ tự sync ✅      │
│   🏠 Về màn hình chính  │
└──────────────────────────┘
```

---

## Verification

- [x] Tắt WiFi → chụp ảnh → QR screen hiện thông báo liên hệ nhân viên + mã số
- [x] Bật WiFi → chụp ảnh → QR screen hiện QR code bình thường
- [x] Mã số trên offline screen khớp với mã số in trên ảnh
- [x] Nút "Về màn hình chính" hoạt động khi offline
- [x] Build thành công
- [x] Tất cả test hiện có vẫn pass

## File List

**New files:**
- `src/PhotoBooth.UI/Services/NetworkCheckService.cs`

**Modified files:**
- `src/PhotoBooth.UI/ViewModels/QRCodeViewModel.cs` — thêm offline detection + properties + D4 catch fallback + F2 cache invalidation + R3 offline timeout fix
- `src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs` — thêm offline detection + properties + D4 catch fallback + F2 cache invalidation + R3 offline timeout fix
- `src/PhotoBooth.UI/Views/QRCodeView.axaml` — thêm offline fallback UI, ẩn QR khi offline
- `src/PhotoBooth.UI/Views/ThankYouView.axaml` — thêm offline fallback UI, ẩn QR + Next button khi offline, F1 fix
- `src/PhotoBooth.UI/PhotoBooth.UI.csproj` — thêm InternalsVisibleTo (undocumented in initial story)
- `src/PhotoBooth.UI/Services/GoogleDriveQRService.cs` — R3 fix cho network timeout retry loop

## Party-Mode Review Findings — Round 1 (Resolved)

| ID | Severity | Finding | Resolution |
|---|---|---|---|
| A1 | 🔴 CRITICAL | HttpClient per call → socket exhaustion | ✅ Fixed: Uses `HttpService.Client` |
| A2 | 🟡 MEDIUM | Static state without thread-safety docs | ✅ Fixed: Documented as benign race |
| A3 | 🟢 LOW | Hardcoded Google endpoint | ✅ Accepted: Google dependency anyway |
| D1 | 🔴 CRITICAL | Missing loading state clear on offline | ✅ Fixed: `IsUploading = false` set |
| D2 | 🟡 MEDIUM | Empty SequentialNumber fallback | ✅ Fixed: Falls back to "---" |
| D3 | 🟡 MEDIUM | ThankYou 3-state conflicts with offline | ✅ Fixed: Next button hidden when offline |
| D4 | 🟢 LOW | Transitional network state handling | ✅ Fixed: Catch block activates offline mode |
| D5 | 🟡 MEDIUM | No unit tests for NetworkCheckService | ✅ Noted: service is too simple for unit tests (static + HTTP) |
| Q1 | 🟡 MEDIUM | Vague acceptance criteria | ✅ Fixed: Tightened AC in story |
| Q2 | 🟡 MEDIUM | No cache expiry verification | ✅ Fixed: Documented 10s delay behavior |
| Q3 | 🟢 LOW | Wrong command name in ThankYou XAML | ✅ Fixed: Uses `GoToThankYouCommand` |
| P1 | 🟡 MEDIUM | "nếu cần" ambiguity | ✅ Fixed: Task 3.6 is mandatory |
| P2 | 🟢 LOW | Incomplete file list | ✅ Fixed: Updated file list |

## Party-Mode Review Findings — Round 2 (Resolved)

| ID | Severity | Finding | Resolution |
|---|---|---|---|
| F1 | 🔴 CRITICAL | ThankYou offline button uses `GoToThankYouCommand` (State 2) instead of `ReturnToStartCommand` — user can't return home | ✅ Fixed: Uses `ReturnToStartCommand` |
| F2 | 🟡 MEDIUM | NetworkCheckService cache not invalidated on catch-fallback → stale "online" for 10s | ✅ Fixed: `ResetCache()` in catch blocks |
| F3 | 🟡 MEDIUM | HttpResponseMessage not disposed → connection pool leak | ✅ Fixed: `using var response` |
| F4 | 🔴 CRITICAL | Button label "Về MH chính" misleads (same root cause as F1) | ✅ Fixed: with F1 |
| F5 | 🟡 MEDIUM | No test coverage for VM offline fallback | ✅ Accepted: ViewModels require Avalonia runtime, not unit-testable without DI refactor |
| F6 | 🟢 LOW | Hardcoded test count "26" in verification | ✅ Fixed: Removed count |

## Party-Mode Review Findings — Round 3 (Resolved)

| ID | Severity | Finding | Resolution |
|---|---|---|---|
| R3.1 | 🔴 CRITICAL | Timeouts bypass Offline Fallback. Both QRUploadService and GoogleDriveQRService throw TaskCanceledException on timeout, which was caught by ViewModels and assumed to be disposal, completely bypassing offline UI. | ✅ Fixed: ViewModels now explicitly check `_cts.IsCancellationRequested` and trigger offline mode on HTTP timeout. |
| R3.2 | 🔴 CRITICAL | Retry loop aborts prematurely on timeouts in GoogleDriveQRService. The HTTP timeout cancellation was being unconditionally rethrown. | ✅ Fixed: Catch block now only rethrows if the parent `ct` (ViewModel) is canceled. |
| R3.3 | 🟡 MEDIUM | NetworkCheckService has a data race on `_lastCheck` (DateTime). | ✅ Fixed: Implemented `lock (_syncRoot)` around cache state. |
| R3.4 | 🟡 MEDIUM | `PhotoBooth.UI.csproj` changed but was undocumented in File List. | ✅ Fixed: Added to File List. |
