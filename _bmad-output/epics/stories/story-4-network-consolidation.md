# Story 4: Tập trung hoá Network (URL + HttpClient)

**Epic:** Sửa Lỗi & Cải thiện Độ ổn định  
**Mức độ:** 🟠 Trung bình  
**Trạng thái:** `done`  
**Lỗi liên quan:** Lỗi 11, 13

---

## Mô tả

URL API ngrok bị hardcode ở ít nhất 4 file khác nhau. Khi URL thay đổi, phải sửa ở nhiều chỗ và dễ sót. Đồng thời, mỗi request tạo `new HttpClient()` mới → gây Socket Exhaustion sau nhiều session.

## Acceptance Criteria

- [x] Tất cả các chỗ gọi API đều dùng `DeviceConfig.ApiBaseUrl` thay vì hardcode URL
- [x] Có một `HttpClient` dùng chung (static/shared) cho toàn bộ ứng dụng UI
- [x] Khi thay đổi URL API, chỉ cần sửa đúng 1 chỗ duy nhất

## Các Tasks

### Task 4.1: Tạo Shared HttpClient ✅

**File MỚI:** `src/PhotoBooth.UI/Services/HttpService.cs`

```csharp
using System;
using System.Net.Http;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Shared HttpClient to avoid socket exhaustion.
/// Uses Lazy initialization for thread-safe singleton pattern.
/// IMPORTANT: Do NOT dispose this client — it is shared across the entire app lifetime.
/// </summary>
public static class HttpService
{
    private static readonly Lazy<HttpClient> _client = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "1");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    });

    public static HttpClient Client => _client.Value;
}
```

### Task 4.2: Thay thế hardcode URL trong BackgroundSelectionViewModel ✅

**File:** [BackgroundSelectionViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/BackgroundSelectionViewModel.cs)

- Đã xoá field `private static readonly string ApiBaseUrl = "..."` (hardcoded)
- Thay `using var client = new HttpClient()` → `var client = HttpService.Client`
- Thay `ApiBaseUrl` → `DeviceConfig.ApiBaseUrl`
- Đã sửa cả `LoadBackgroundsAsync()` lẫn `LoadBitmapFromUrl()`

### Task 4.3: Thay thế hardcode URL trong PhotoSelectionViewModel ✅

**File:** [PhotoSelectionViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs)

Đã sửa 3 chỗ hardcode URL (lines 179, 317, 419) → `DeviceConfig.ApiBaseUrl`
Đã thay `new HttpClient()` / `new System.Net.Http.HttpClient()` → `HttpService.Client`

### Task 4.4: Thay thế `new HttpClient()` trong các ViewModel khác ✅

Áp dụng tương tự cho:
- [StartViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/StartViewModel.cs) — `CheckDeviceStatusAsync()` ✅
- [ThankYouViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs) — `UploadAndGenerateQR()` ✅
- [QRCodeViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/QRCodeViewModel.cs) — `UploadAndGenerateQR()` ✅

**Lưu ý:** Khi dùng shared HttpClient, **KHÔNG** `using var client = ...` vì HttpClient shared không được dispose.

## Verification

- [x] Tìm kiếm toàn bộ source code: không còn `"https://intellective"` ngoại trừ trong `DeviceConfig.cs` và `ApiService.cs` (Admin app)
- [x] Build thành công — 0 errors
- [ ] Chạy ứng dụng, chụp ảnh, tải QR → vẫn hoạt động bình thường
- [ ] Chạy 20 session liên tiếp → không lỗi kết nối mạng (socket exhaustion)
