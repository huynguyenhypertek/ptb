# Story 4.3: Generate QR and Embed Image

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want the QR code to be automatically generated and burned directly into my final photo composite,
so that I can easily scan the printed photo later without needing a separate on-screen QR step, and the flow is streamlined.

## Acceptance Criteria

1. **Service Refactoring for QR Bytes (`GoogleDriveQRService.cs`)**:
   - Create a new method `WaitSyncAndGenerateQRBytesAsync(string sessionFolderName, byte[] foregroundColor, byte[] backgroundColor, Action<string>? onStatusUpdate = null, CancellationToken ct = default)` that returns `Task<(byte[]? QrBytes, string StatusText, bool Success)>`.
   - Refactor the existing `WaitSyncAndGenerateQRAsync` to call `WaitSyncAndGenerateQRBytesAsync`. If successful, it should wrap the returned `byte[]` in a `System.IO.MemoryStream` and create an Avalonia `Bitmap` from it, ensuring backward compatibility with `PhotoBooth.UI`.

2. **Service Refactoring for QR Bytes (`QRUploadService.cs`)**:
   - Create a new method `UploadAndGenerateQRBytesAsync(string imagePath, byte[] foregroundColor, byte[] backgroundColor, CancellationToken ct = default)` that returns `Task<(byte[]? QrBytes, string StatusText, bool Success)>`.
   - Refactor the existing `UploadAndGenerateQRAsync` to call `UploadAndGenerateQRBytesAsync` and wrap the bytes into an Avalonia `Bitmap`.

3. **ReviewPrintViewModel Implementation (`ReviewPrintViewModel.cs`)**:
   - Implement the placeholder `GenerateQROverlayAsync(CancellationToken ct)`.
   - **Network Check**: Call `NetworkCheckService.IsOnlineAsync()`. If offline, set `IsOffline = true`, `StatusText = "📴 Không có kết nối mạng"`, and return early.
   - **Google Drive Flow (`DeviceConfig.GoogleDriveEnabled == true`)**:
     - Check `SessionService.CurrentSession.PreFetchedDriveUrl`.
     - If available (instant QR): Generate the QR code bytes directly using `QRCoder.QRCodeGenerator` and `QRCoder.PngByteQRCode` (use `ECCLevel.M` and a 20px graphic).
     - If not available: Call `GoogleDriveQRService.WaitSyncAndGenerateQRBytesAsync` with `SessionService.CurrentSession.SessionFolderName`.
   - **ngrok Fallback Flow (`DeviceConfig.GoogleDriveEnabled == false`)**:
     - Call `QRUploadService.UploadAndGenerateQRBytesAsync` using `SessionService.CurrentSession.FinalImagePath`.

4. **OpenCV QR Embedding**:
   - Once `qrBytes` are successfully obtained, call `ImageCompositeService.OverlayQrCode(SessionService.CurrentSession.FinalImagePath, qrBytes)`.
   - This burns the QR code directly onto the composite image file.

5. **UI Reload & Error Handling**:
   - **Success**: If `OverlayQrCode` succeeds, call `LoadFinalImage()` to refresh the `FinalImage` bitmap (ensure the old bitmap is `Dispose()`d before reloading). Update `StatusText` to the success message.
   - **Failure/Timeout**: 
     - Catch `OperationCanceledException`. If not caused by `_cts.IsCancellationRequested`, it means a timeout occurred. Call `NetworkCheckService.ResetCache()`, set `IsOffline = true`, and `StatusText = "📴 Lỗi kết nối mạng"`.
     - Catch general `Exception`. Log the error type (do NOT log `ex.Message` as it may contain Google Apps Script deployment keys), call `NetworkCheckService.ResetCache()`, set `IsOffline = true`, and `StatusText = "📴 Lỗi kết nối mạng"`.

## Tasks / Subtasks

- [x] Task 1: Refactor `GoogleDriveQRService`
  - [x] Add `WaitSyncAndGenerateQRBytesAsync` returning `byte[]`.
  - [x] Modify `WaitSyncAndGenerateQRAsync` to use the new method.
- [x] Task 2: Refactor `QRUploadService`
  - [x] Add `UploadAndGenerateQRBytesAsync` returning `byte[]`.
  - [x] Modify `UploadAndGenerateQRAsync` to use the new method.
- [x] Task 3: Implement `GenerateQROverlayAsync` in `ReviewPrintViewModel`
  - [x] Add network check using `NetworkCheckService`.
  - [x] Add Google Drive logic (Prefetch vs WaitSync).
  - [x] Add ngrok fallback logic (`QRUploadService`).
  - [x] Handle Timeout/Exceptions safely (reset cache, set offline).
- [x] Task 4: Connect OpenCV and UI Update
  - [x] Call `ImageCompositeService.OverlayQrCode` with the byte array.
  - [x] Reload `FinalImage` UI property, disposing the old instance.

## Dev Notes

- **Architecture Compliance (CRITICAL)**: 
  - `ImageCompositeService` must NOT reference Avalonia `Bitmap`. It is decoupled from UI. This is exactly why we must refactor the QR services to return `byte[]`.
  - Do NOT modify the `QRCoder` usage parameters (`ECCLevel.M`, graphic size 20, solid white background) as these are tested for maximum scannability.
- **Memory Management (CRITICAL)**: 
  - When reloading the `FinalImage` property, you MUST call `Dispose()` on the previous instance of the Avalonia `Bitmap` to prevent memory leaks.
- **Security (CRITICAL)**: 
  - When catching exceptions during the API call to Google Apps Script, do NOT log `ex.Message`. It might contain sensitive URL parameters (like deployment keys). Log `ex.GetType().Name` instead, as seen in `ThankYouViewModel.cs`.

### Project Structure Notes

- Files to modify: 
  - `src/PhotoBooth.UI/Services/GoogleDriveQRService.cs`
  - `src/PhotoBooth.UI/Services/QRUploadService.cs`
  - `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs`

### References

- [Source: src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs#L75-L198] for exact implementation of network checks, exception catching, and Google Drive prefetch logic.
- [Source: src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs] for the OpenCV method `OverlayQrCode` added in E4-S2.

## Dev Agent Record

### Agent Model Used
Gemini 3.1 Pro (High)

### Debug Log References
- No regressions found.
- Previous agent completely successfully implemented the requirements in previous sessions.

### Completion Notes List
- Refactored `GoogleDriveQRService.WaitSyncAndGenerateQRBytesAsync` to return `byte[]`.
- Refactored `QRUploadService.UploadAndGenerateQRBytesAsync` to return `byte[]`.
- Implemented `GenerateQROverlayAsync` in `ReviewPrintViewModel.cs` combining both Google Drive and ngrok strategies.
- Integrated OpenCV using `ImageCompositeService.OverlayQrCode` and securely reloaded `FinalImage` with proper `Dispose()`.
- [AI-Review] Fixed HIGH performance issue: Moved synchronous `OverlayQrCode` and `LoadFinalImage` I/O to background threads in `ReviewPrintViewModel`.
- [AI-Review] Fixed HIGH missing test issue: Added test for `LoadFinalImageAsync` in `ReviewPrintViewModelTests`.

### File List
- `src/PhotoBooth.UI/Services/GoogleDriveQRService.cs`
- `src/PhotoBooth.UI/Services/QRUploadService.cs`
- `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs`
- `src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs`
- `tests/PhotoBooth.Tests/ViewModels/ReviewPrintViewModelTests.cs`
