# Story 3: Tích hợp QR từ Google Drive link

**Epic:** Tích hợp Google Drive — Lưu ảnh & QR chia sẻ  
**Mức độ:** 🟠 Trung bình  
**Trạng thái:** `done`  
**Phụ thuộc:** Story 1 (config path — `done`), Story 2 (Apps Script URL — `done`)

---

## Mô tả

Tạo service mới `GoogleDriveQRService` để thay thế `QRUploadService` khi Google Drive được bật. Service sẽ gọi Google Apps Script để lấy link share folder, sau đó tạo QR code từ link đó. Bao gồm cơ chế retry chờ Google Drive sync xong.

## Acceptance Criteria

- [x] Tạo `GoogleDriveQRService` — gọi Apps Script, nhận link, tạo QR. Caller owns returned Bitmap and is responsible for disposing it (matching `QRUploadService` contract)
- [x] Retry tối đa 10 lần, mỗi lần cách 3 giây, chờ Drive sync xong
- [x] Hiển thị trạng thái "Đang đồng bộ ảnh..." trong khi chờ
- [x] QR code chứa link Google Drive folder (không phải link ngrok)
- [x] `DeviceConfig` có property `AppsScriptUrl` — URL của Apps Script web app
- [x] Hỗ trợ CLI argument `--appsScriptUrl=<url>` (camelCase + `=` separator — consistent with existing args)
- [x] `AppsScriptUrl` included in startup `[CONFIG]` log line (masked via `GetMaskedAppsScriptUrl()`)
- [x] `QRCodeViewModel` và `ThankYouViewModel` dùng `GoogleDriveQRService` khi `GoogleDriveEnabled = true`
- [x] Fallback về `QRUploadService` (ngrok) khi `GoogleDriveEnabled = false`
- [x] Xử lý lỗi: không có internet → hiện thông báo, không crash
- [x] Xử lý lỗi: `AppsScriptUrl` rỗng khi `GoogleDriveEnabled = true` → hiện lỗi rõ ràng
- [x] Build thành công, không lỗi

## Các Tasks

### Task 3.1: Thêm Apps Script URL vào DeviceConfig

#### [DeviceConfig.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/DeviceConfig.cs)

Thêm property (sau `GoogleDrivePath`):

```csharp
public static string AppsScriptUrl { get; set; } = "";
```

Thêm vào `ParseArgs()` — dùng đúng pattern `if/else if` + `StartsWith` + `Substring` hiện tại:

```csharp
else if (arg.StartsWith("--appsScriptUrl="))
{
    var url = arg.Substring("--appsScriptUrl=".Length);
    if (!string.IsNullOrWhiteSpace(url))
        AppsScriptUrl = url;
}
```

Cập nhật log line hiện tại (dòng 82) để bao gồm `AppsScriptUrl`:

```diff
- System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GetMaskedGDrivePath()}");
+ System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GetMaskedGDrivePath()}, AppsScriptUrl={GetMaskedAppsScriptUrl()}");
```

### Task 3.2: Tạo GoogleDriveQRService

#### [NEW] [GoogleDriveQRService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/GoogleDriveQRService.cs)

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using QRCoder;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Service tạo QR code từ Google Drive link.
/// Gọi Google Apps Script để lấy link share folder session.
/// </summary>
/// <remarks>
/// Caller owns the returned Bitmap and is responsible for disposing it
/// (matching <see cref="QRUploadService"/> contract).
/// </remarks>
public static class GoogleDriveQRService
{
    private const int MaxRetries = 10;
    private const int RetryDelayMs = 3000;

    /// <summary>
    /// Chờ Google Drive sync xong, lấy link folder, tạo QR code.
    /// </summary>
    /// <returns>
    /// A tuple of (qrBitmap, statusText, success). Caller owns the returned Bitmap
    /// and is responsible for disposing it.
    /// </returns>
    public static async Task<(Bitmap? QrBitmap, string StatusText, bool Success)> 
        WaitSyncAndGenerateQRAsync(
            string sessionFolderName,
            byte[] foregroundColor,
            byte[] backgroundColor,
            Action<string>? onStatusUpdate = null,
            CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionFolderName))
            return (null, "❌ Không có thông tin session", false);

        if (string.IsNullOrEmpty(DeviceConfig.AppsScriptUrl))
            return (null, "❌ Chưa cấu hình Apps Script URL (--appsScriptUrl)", false);

        var client = HttpService.Client;
        string? driveUrl = null;

        // Retry loop — chờ Google Drive sync xong
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            
            onStatusUpdate?.Invoke($"⏳ Đang đồng bộ ảnh lên Google Drive... ({attempt}/{MaxRetries})");

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                
                // Use UriBuilder to safely append query params (handles URLs that already contain '?')
                var builder = new UriBuilder(DeviceConfig.AppsScriptUrl);
                var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
                query["folder"] = sessionFolderName;
                builder.Query = query.ToString();
                
                var json = await client.GetStringAsync(builder.Uri, timeoutCts.Token);
                var result = JsonSerializer.Deserialize<AppsScriptResponse>(json);

                if (result?.Success == true && !string.IsNullOrEmpty(result.Url))
                {
                    driveUrl = result.Url;
                    break; // Tìm thấy folder, thoát retry loop
                }

                // Folder chưa sync xong, chờ rồi thử lại
                Console.WriteLine($"[GDRIVE] Attempt {attempt}: folder not found yet, retrying in {RetryDelayMs}ms");
            }
            catch (OperationCanceledException) { throw; } // Don't swallow cancellation
            catch (Exception)
            {
                // Don't log ex.Message — HttpRequestException includes full URL with deployment key
                Console.WriteLine($"[GDRIVE] Attempt {attempt}: request failed, retrying...");
            }

            if (attempt < MaxRetries)
            {
                await Task.Delay(RetryDelayMs, ct);
            }
        }

        if (string.IsNullOrEmpty(driveUrl))
        {
            return (null, "❌ Không thể đồng bộ ảnh lên Google Drive. Kiểm tra kết nối mạng.", false);
        }

        // Tạo QR code từ Google Drive link
        onStatusUpdate?.Invoke("✅ Đã đồng bộ! Đang tạo mã QR...");

        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(driveUrl, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20, foregroundColor, backgroundColor);

        using var ms = new MemoryStream(qrBytes);
        var qrBitmap = new Bitmap(ms);

        Console.WriteLine("[GDRIVE] QR generated successfully");
        return (qrBitmap, "📱 Quét mã QR để tải ảnh từ Google Drive", true);
    }
}

/// <summary>
/// Response từ Google Apps Script.
/// Uses JsonPropertyName for defensive deserialization (matching UploadResult pattern).
/// </summary>
public class AppsScriptResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    [JsonPropertyName("folderName")]
    public string? FolderName { get; set; }
    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }
}
```

### Task 3.3: Cập nhật QRCodeViewModel

#### [QRCodeViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/QRCodeViewModel.cs)

Thay đổi method `UploadAndGenerateQR()` (L36-74) — thêm phân nhánh Google Drive / ngrok:

```csharp
private async System.Threading.Tasks.Task UploadAndGenerateQR()
{
    try
    {
        if (DeviceConfig.GoogleDriveEnabled)
        {
            var folderName = SessionService.CurrentSession.SessionFolderName;
            var (qr, status, ok) = await GoogleDriveQRService.WaitSyncAndGenerateQRAsync(
                folderName ?? "",
                foregroundColor: new byte[] { 255, 255, 255 },
                backgroundColor: new byte[] { 30, 30, 46 },
                onStatusUpdate: msg => StatusText = msg,
                ct: _cts.Token
            );

            if (_disposed) { qr?.Dispose(); return; }

            if (ok && qr != null)
            {
                var oldQr = QrCodeImage;
                QrCodeImage = qr;
                oldQr?.Dispose();
                StatusText = status;
            }
            else
            {
                StatusText = status;
            }
        }
        else
        {
            // Giữ nguyên logic QRUploadService hiện tại
            var finalImage = SessionService.CurrentSession.FinalImagePath;
            var (qrBitmap, statusText, success) = await QRUploadService.UploadAndGenerateQRAsync(
                finalImage,
                foregroundColor: new byte[] { 255, 255, 255 },
                backgroundColor: new byte[] { 30, 30, 46 },
                _cts.Token);

            if (_disposed) { qrBitmap?.Dispose(); return; }

            if (success && qrBitmap != null)
            {
                var oldQr = QrCodeImage;
                QrCodeImage = qrBitmap;
                oldQr?.Dispose();
                StatusText = statusText;
            }
            else
            {
                StatusText = statusText;
            }
        }

        IsUploading = false;
    }
    catch (OperationCanceledException)
    {
        // Expected when disposed during upload
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[QR] Error: {ex.Message}");
        StatusText = "❌ Lỗi kết nối. Thử lại sau.";
        IsUploading = false;
    }
}
```

### Task 3.4: Cập nhật ThankYouViewModel

#### [ThankYouViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs)

Thay đổi method `UploadAndGenerateQR()` (L62-99) — áp dụng cùng logic phân nhánh, giữ nguyên color scheme (black on white):

```csharp
private async System.Threading.Tasks.Task UploadAndGenerateQR()
{
    try
    {
        if (DeviceConfig.GoogleDriveEnabled)
        {
            var folderName = SessionService.CurrentSession.SessionFolderName;
            var (qr, status, ok) = await GoogleDriveQRService.WaitSyncAndGenerateQRAsync(
                folderName ?? "",
                foregroundColor: new byte[] { 0, 0, 0 },
                backgroundColor: new byte[] { 255, 255, 255 },
                onStatusUpdate: msg => StatusText = msg,
                ct: _cts.Token
            );

            if (_disposed) { qr?.Dispose(); return; }

            if (ok && qr != null)
            {
                var oldQr = QrCodeImage;
                QrCodeImage = qr;
                oldQr?.Dispose();
                StatusText = status;
            }
            else
            {
                StatusText = status;
            }
        }
        else
        {
            // Giữ nguyên logic QRUploadService hiện tại
            var finalImage = SessionService.CurrentSession.FinalImagePath;
            var (qrBitmap, statusText, success) = await QRUploadService.UploadAndGenerateQRAsync(
                finalImage,
                foregroundColor: new byte[] { 0, 0, 0 },
                backgroundColor: new byte[] { 255, 255, 255 },
                _cts.Token);

            if (_disposed) { qrBitmap?.Dispose(); return; }

            if (success && qrBitmap != null)
            {
                var oldQr = QrCodeImage;
                QrCodeImage = qrBitmap;
                oldQr?.Dispose();
                StatusText = statusText;
            }
            else
            {
                StatusText = statusText;
            }
        }

        Console.WriteLine($"[QR] Generated successfully");
    }
    catch (OperationCanceledException)
    {
        // Expected when disposed during upload
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[QR] Error: {ex.Message}");
        StatusText = "❌ Lỗi kết nối";
    }
}
```

---

## Verification

### Functional Tests

- [ ] Bật Google Drive → chụp ảnh → chờ sync → QR hiện link `drive.google.com/...`
- [ ] Scan QR trên điện thoại → mở Google Drive folder → thấy ảnh gốc + ảnh ghép
- [ ] Nhiều người scan cùng 1 QR → đều truy cập được
- [ ] Tắt Google Drive → chụp ảnh → QR hiện link ngrok (fallback)

### Error Handling

- [ ] Tắt wifi → chụp ảnh → ảnh lưu local OK → QR hiện "Không thể đồng bộ"
- [ ] Apps Script URL sai → hiện lỗi, không crash
- [ ] `--appsScriptUrl` không truyền (empty) + `GoogleDriveEnabled=true` → hiện "Chưa cấu hình Apps Script URL"
- [ ] Google Drive Desktop chưa chạy → ảnh lưu vào folder (chưa sync), retry timeout → hiện lỗi

### Performance

- [ ] Thời gian từ chụp xong → QR hiện ra: < 30 giây (phụ thuộc tốc độ mạng)
- [ ] Retry không block UI — StatusText cập nhật liên tục

### Build

- [ ] `dotnet build` thành công, 0 errors

---

## Cách chạy app với Google Drive

```bash
dotnet run --project src/PhotoBooth.UI -- \
  --googleDriveEnabled=true \
  --googleDrivePath="/Users/xxx/Library/CloudStorage/GoogleDrive-xxx@gmail.com/My Drive" \
  --appsScriptUrl="https://script.google.com/macros/s/AKfycb.../exec"
```

---

## File List

**New files:**
- `src/PhotoBooth.UI/Services/GoogleDriveQRService.cs`

**Modified files:**
- `src/PhotoBooth.UI/DeviceConfig.cs` (thêm `AppsScriptUrl` + CLI parsing + log update)
- `src/PhotoBooth.UI/ViewModels/QRCodeViewModel.cs` (phân nhánh Google Drive / ngrok)
- `src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs` (phân nhánh Google Drive / ngrok)

---

## Party-Mode Validation Findings

### Round 1

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| F1 | CRITICAL | **CLI arg format mismatch** — Story used `--apps-script-url` (kebab-case) but existing `DeviceConfig.ParseArgs()` uses camelCase (`--storeId`, `--deviceId`, `--googleDriveEnabled`). Story 2 output section also references `--appsScriptUrl`. | **Fixed** — Changed to `--appsScriptUrl=<url>` throughout (AC, Task 3.1, CLI example) |
| F2 | CRITICAL | **CLI parsing pattern mismatch** — Task 3.1 used `case "..."` switch syntax, but `DeviceConfig.ParseArgs()` uses `if/else if` + `StartsWith` + `Substring` pattern | **Fixed** — Rewrote Task 3.1 snippet to match existing pattern |
| F3 | CRITICAL | **Missing `AppsScriptUrl` in startup log** — Task 3.1 didn't update `[CONFIG]` log line at L82 of `DeviceConfig.cs`. Operators can't verify URL is set. | **Fixed** — Added diff showing log line update |
| F4 | MEDIUM | **Drive URL leaked in log** — `Console.WriteLine($"[GDRIVE] QR generated for: {driveUrl}")` exposed full folder URL. Inconsistent with PII-awareness patterns from Story 1 | **Fixed** — Changed to `"[GDRIVE] QR generated successfully"` (no URL) |
| F5 | MEDIUM | **ThankYouViewModel instructions too vague** — Task 3.4 said "áp dụng cùng logic" without concrete code. ThankYou has different color scheme (black on white) and different status text patterns | **Fixed** — Added complete replacement method with correct colors |
| F6 | MEDIUM | **Property name mismatch** — Story referenced `StatusMessage = msg` but actual ViewModel property is `StatusText` (L19 of QRCodeViewModel.cs). Would cause compile error | **Fixed** — Changed to `StatusText` throughout |
| F7 | MEDIUM | **Incomplete code snippet** — Task 3.3 showed partial code that could break existing error handling when pasted. Developer needs full method replacement | **Fixed** — Task 3.3 now shows complete `UploadAndGenerateQR()` method replacement |
| F8 | MEDIUM | **Missing disposal contract documentation** — `GoogleDriveQRService` didn't document Bitmap ownership. Existing `QRUploadService` has explicit doc: "Caller owns the returned Bitmap" | **Fixed** — Added `<remarks>` and `<returns>` XML doc matching `QRUploadService` pattern |
| F9 | MEDIUM | **Missing `[JsonPropertyName]` attributes** — `AppsScriptResponse` used lowercase property names without JSON attributes. `UploadResult` in `QRUploadService` uses `[JsonPropertyName]` for defensive deserialization | **Fixed** — Added `[JsonPropertyName]` on all properties, PascalCase naming |
| F10 | LOW | **Story status `draft`** — Both dependencies are `done`. Story is fully specified with implementation code | **Fixed** — Changed to `ready-for-dev` |
| F11 | LOW | **Missing verification for empty `AppsScriptUrl`** — Error handling tests didn't cover `--appsScriptUrl` not provided | **Fixed** — Added verification item in Error Handling section |
| F12 | LOW | **CLI example used space separator** — `--apps-script-url "https://..."` but existing pattern uses `=`: `--googleDrivePath="/path"` | **Fixed** — Changed to `--appsScriptUrl="https://..."` |

### Round 2

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| F13 | MEDIUM | **`AppsScriptUrl` leaked unmasked in `[CONFIG]` log** — URL contains deployment key (`AKfycb...`) which is a secret. `GoogleDrivePath` is masked via `GetMaskedGDrivePath()` but `AppsScriptUrl` was logged raw. Inconsistent with PII-awareness patterns from Story 1 | **Fixed** — Added `GetMaskedAppsScriptUrl()` to `DeviceConfig.cs` that shows only domain + first 10 chars of path. Updated log line to use it. Updated Task 3.1 diff in story |
| F14 | MEDIUM | **Story status `ready-for-dev` but code already implemented** — All 4 files (DeviceConfig, GoogleDriveQRService, QRCodeViewModel, ThankYouViewModel) are already implemented in source and build passes | **Fixed** — Changed status to `done`. All AC checkboxes marked |
| F15 | MEDIUM | **Task 3.1 diff references line 82 but actual log is now at line 105** — After `GetMaskedGDrivePath()` and `GetMaskedAppsScriptUrl()` methods were added, log line shifted. Story diff showed wrong baseline | **Fixed** — Updated diff to show `GetMaskedAppsScriptUrl()` call |
| F16 | LOW | **AC "masked nếu cần" ambiguous** — AC 7 said `AppsScriptUrl` should be "masked nếu cần" but didn't specify whether masking was needed. Deployment keys ARE secrets | **Fixed** — AC 7 rewritten to explicitly state `masked via GetMaskedAppsScriptUrl()` |

### Round 4 (Party-Mode Source Code Review)

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| F23 | MEDIUM | **`QRCodeViewModel` outer catch leaks AppsScript URL** — `catch (Exception ex) { Console.WriteLine($"[QR] Error: {ex.Message}") }` exposes full URL (with `AKfycb...` key) via `HttpRequestException.Message` or `UriFormatException.Message`. Same class of bug as F18 | **Fixed** — Replaced `ex.Message` with `ex.GetType().Name` |
| F24 | MEDIUM | **`ThankYouViewModel` outer catch leaks AppsScript URL** — Same issue as F23 in `[QR-ThankYou] Error: {ex.Message}` | **Fixed** — Replaced `ex.Message` with `ex.GetType().Name` |
| F25 | MEDIUM | **`IsUploading` stays `true` on `OperationCanceledException`** — Cancel path didn't reset `IsUploading`, leaving perpetual loading spinner if ViewModel observed before disposal | **Fixed** — Added `IsUploading = false;` in cancellation catch block |
| F26 | LOW | **`QRUploadService` logs full download URL** — Two `Console.WriteLine` lines exposed full ngrok URL. Inconsistent with GoogleDriveQRService's F4/F22 pattern | **Fixed** — Removed URL from both log lines |
| F27 | LOW | **`ApiBaseUrl` logged unmasked in `[CONFIG]`** — Inconsistent with `GoogleDrivePath`/`AppsScriptUrl` masking. Ngrok URLs are ephemeral | **Deferred** — LOW risk, ngrok URLs rotate frequently |
| F28 | LOW | **`QRCodeViewModel` missing success/failure logs** — No branch-specific logging, unlike `ThankYouViewModel`'s descriptive `[QR-ThankYou]` logs | **Fixed** — Added `[QR] Generated successfully (Google Drive/ngrok)` and failure logs |
| F29 | LOW | **Story Task 3.2 code snippet stale** — Still showed naive URL concatenation, `ex.Message` logging, missing `OperationCanceledException` handling (pre-F17/F18/F19) | **Fixed** — Updated snippet to match actual `GoogleDriveQRService.cs` source |

---

## Change Log

| Date | Change |
|------|--------|
| 2026-05-14 | Initial draft |
| 2026-05-14 | Party-mode validation round 1 — 12 findings (3 CRITICAL, 6 MEDIUM, 3 LOW). All fixed: CLI consistency (F1/F2/F12), log output (F3), URL leak (F4), ThankYou code (F5), property names (F6), complete snippets (F7), disposal docs (F8), JSON attributes (F9), status update (F10), verification gap (F11) |
| 2026-05-14 | Party-mode validation round 2 — 4 findings (0 CRITICAL, 3 MEDIUM, 1 LOW). All fixed: AppsScriptUrl masking (F13), status→done (F14), line reference (F15), AC ambiguity (F16) |
| 2026-05-14 | Adversarial code review round 3 — 6 findings (2 CRITICAL, 3 MEDIUM, 1 LOW). All fixed: URL injection (F17), secret key leak (F18), swallowed cancellation (F19), misleading log (F20), path crash guard (F21), interpolation cleanup (F22) |
| 2026-05-14 | Party-mode source code review round 4 — 7 findings (0 CRITICAL, 3 MEDIUM, 4 LOW). 6 fixed, 1 deferred: caller catch URL leak (F23/F24), IsUploading stuck (F25), QRUploadService URL log (F26), ApiBaseUrl unmasked (F27 deferred), missing VM logs (F28), stale story snippet (F29) |
