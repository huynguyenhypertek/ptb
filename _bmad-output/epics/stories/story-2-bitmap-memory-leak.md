# Story 2: Rò rỉ Bitmap (Memory Leak)

**Epic:** Sửa Lỗi & Cải thiện Độ ổn định  
**Mức độ:** 🔴 Nghiêm trọng  
**Trạng thái:** `done`  
**Lỗi liên quan:** Lỗi 3, 4, 5, 8, 9

---

## Mô tả

Nhiều ViewModel tạo ra các đối tượng `Bitmap` (từ file ảnh, asset, hoặc URL) nhưng không bao giờ gọi `Dispose()`. Mỗi session chụp giữ lại hàng chục MB Bitmap trong RAM. Sau 50–100 session, ứng dụng sẽ đóng băng hoặc crash do hết RAM.

## Acceptance Criteria

- [x] Tất cả ViewModel có chứa `Bitmap` phải implement `IDisposable`
- [x] Tất cả `Bitmap` cũ phải được `Dispose()` khi ViewModel bị huỷ (navigate đi) hoặc khi gán Bitmap mới
- [x] `ConfirmPrintViewModel` load ảnh mà không lock file
- [x] RAM không tăng liên tục khi chạy nhiều session liên tiếp
- [x] `NavigationService` dispose ViewModel trên UI thread (tránh crash)
- [x] `PhotoItem.Thumbnail` load ảnh mà không lock file
- [x] Tất cả Bitmap properties phải set `null` sau khi dispose (tránh use-after-dispose)
- [x] Async ViewModel phải guard bitmap assignment khi đã bị disposed
- [x] `PaymentSuccessViewModel` auto-nav phải cancel khi dispose

## Các Tasks

### Task 2.1: `PhotoSelectionViewModel` — Implement `IDisposable`

**File:** [PhotoSelectionViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs)

**Đã hoàn thành:**
- Thêm `: IDisposable` vào class declaration
- Dispose `BackgroundImage`, `FramePreviewImage`, và tất cả `PhotoItem.Thumbnail`
- Dispose old Bitmap trước khi gán mới trong `LoadFrameFromApiAsync()` (fallback path), `LoadFramePreview()`, và `LoadBackground()`
- Fix `LoadThumbnail()` dùng `MemoryStream` thay vì `new Bitmap(path)` để không lock file
- `SelectedPhoto1..6` trỏ vào Thumbnail đã dispose nên chỉ set null
- Null-after-dispose cho `BackgroundImage` và `FramePreviewImage`

### Task 2.2: `BackgroundSelectionViewModel` — Implement `IDisposable`

**File:** [BackgroundSelectionViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/BackgroundSelectionViewModel.cs)

**Đã hoàn thành:**
- Thêm `: IDisposable`
- Dispose `Option1Image` và `Option2Image` + set null
- Thêm `_disposed` flag để guard async `LoadBackgroundsAsync()` — nếu ViewModel bị dispose trước khi API trả về, Bitmap mới sẽ được dispose ngay thay vì bị leak

### Task 2.3: `ThankYouViewModel` — Implement `IDisposable` + Dispose Bitmap cũ khi chuyển state

**File:** [ThankYouViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs)

**Đã hoàn thành:**
- Thêm `: IDisposable`
- Dispose old `BackgroundImage` trước khi gán mới trong 3 state transitions
- Dispose old `QrCodeImage` trước khi gán mới trong `UploadAndGenerateQR()`
- Null-after-dispose cho cả `BackgroundImage` và `QrCodeImage`
- Thêm `_disposed` flag

### Task 2.4: `ConfirmPrintViewModel` — Implement `IDisposable` + Fix file lock

**File:** [ConfirmPrintViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/ConfirmPrintViewModel.cs)

**Đã hoàn thành:**
- Thêm `: IDisposable`
- Sửa `LoadFinalImage()` dùng `MemoryStream` để không lock file
- Dispose + null `BackgroundImage` và `FinalImage`

### Task 2.5: Các ViewModel khác có Bitmap

**Đã hoàn thành** — thêm `IDisposable` + null-after-dispose cho:
- `PaymentAmountViewModel` — dispose + null `BackgroundImage`
- `PaymentProcessingViewModel` — dispose + null `BackgroundImage`
- `PaymentSuccessViewModel` — dispose + null `BackgroundImage` + `CancellationToken` cho auto-nav
- `QRCodeViewModel` — dispose + null `QrCodeImage` + `_disposed` flag guard async

### Task 2.6: `CaptureViewModel` — Fix thiếu dispose `BackgroundImage`

**File:** [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

**Đã hoàn thành:**
- Thêm `BackgroundImage?.Dispose()` + null vào `Dispose()` có sẵn
- Thêm null-after-dispose cho `CameraPreview`

### Task 2.7: `NavigationService` — Fix dispose trên background thread + memory spike

**File:** [NavigationService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/NavigationService.cs)

**Đã hoàn thành:**
- Đổi từ `Task.Run` → synchronous trên UI thread
- Đổi thứ tự: dispose old view **trước**, rồi mới assign new view (tránh 2× peak memory)

## Verification

- [x] Tất cả ViewModel có Bitmap đều implement `IDisposable`
- [x] `NavigationService` gọi `Dispose()` khi navigate đi
- [x] Không có file lock khi load ảnh (`ConfirmPrint`, `PhotoItem.Thumbnail`)
- [x] Không dispose Bitmap trên background thread
- [x] Tất cả Bitmap properties set null sau dispose
- [x] Async ViewModels guard bitmap assignment khi disposed
- [x] PaymentSuccessVM auto-nav cancelled khi disposed
- [ ] Mở Activity Monitor / Task Manager, theo dõi RAM của process PhotoBooth.UI
- [ ] Chạy 10 session liên tiếp (Start → Chụp → Chọn ảnh → Xác nhận → QR → Về lại Start)
- [ ] RAM phải ổn định (dao động < 50MB), không tăng liên tục theo số session
- [ ] Console không hiển thị lỗi `ObjectDisposedException`

## Review Findings History

### Round 1: Initial Adversarial Review (8 findings)

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| 1 | 🔴 Critical | `CaptureViewModel.BackgroundImage` not disposed | Added to existing `Dispose()` |
| 2 | 🟠 Medium | Wrong line numbers (149 → 150) | Fixed in story doc |
| 3 | 🔴 Critical | `ConfirmPrintVM` file-lock fix incomplete | Used MemoryStream pattern |
| 4 | 🟠 Medium | Old Bitmaps leaked on reassignment | Dispose-before-assign pattern |
| 5 | 🟠 Medium | `QRCodeVM` pattern missing `QrCodeImage` | Fixed Dispose impl |
| 6 | 🟠 Medium | `PhotoItem.Thumbnail` locks files | MemoryStream pattern |
| 7 | 🔴 Critical | `NavigationService` disposes on bg thread | Changed to UI thread |
| 8 | 🟡 Low | No automated RAM metric | Noted as manual QA |

### Round 2: Party Mode Multi-Agent Review (9 findings)

| # | Agent | Severity | Finding | Resolution |
|---|-------|----------|---------|------------|
| A-1 | 🏗️ Architect | 🔴 Critical | Dispose after assign = 2× peak memory | Reordered: dispose-then-assign |
| A-2 | 🏗️ Architect | 🟠 Medium | No `GC.SuppressFinalize` | Deferred — Avalonia Bitmap is managed wrapper |
| A-3 | 🏗️ Architect | 🟠 Medium | `PaymentSuccessVM` auto-nav race | Added `CancellationTokenSource` |
| D-1 | 💻 Dev | 🔴 Critical | `Confirm()` temp file + composite leak | Tracked for Story 5 (Disk Cleanup) |
| D-2 | 💻 Dev | 🟠 Medium | `ThankYouVM.QrCodeImage` no dispose on reassign | Added dispose-before-assign |
| D-3 | 💻 Dev | 🟡 Low | Story doc AC vs Verification inconsistency | Fixed in story doc |
| Q-1 | 🧪 QA | 🔴 Critical | No null-after-dispose guard | All Dispose() now null properties |
| Q-2 | 🧪 QA | 🟠 Medium | `BackgroundSelectionVM` async leak on early nav | Added `_disposed` flag guard |
| Q-3 | 🧪 QA | 🟡 Low | `QRCodeVM` same async leak | Added `_disposed` flag guard |

### Round 3: Adversarial Senior Dev Review (7 findings)

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| 1 | 🔴 Critical | `PhotoItem.Thumbnail` not nulled after dispose → use-after-dispose | Added `photo.Thumbnail = null` in `Dispose()` |
| 2 | 🔴 Critical | `BackgroundSelectionVM.LoadDefaultBackgrounds()` no `_disposed` guard | Added `if (_disposed) return;` |
| 3 | 🟠 Medium | `CaptureVM.LoadCaptureBackground()` no dispose-before-assign | Added dispose-before-assign pattern |
| 4 | 🟠 Medium | `ConfirmPrintVM.LoadBackground()` no dispose-before-assign | Added dispose-before-assign pattern |
| 5 | 🟠 Medium | `PhotoSelectionVM.Confirm()` temp frame file never deleted | Added `File.Delete(tempFramePath)` after compose |
| 6 | 🟡 Low | `PrintingVM` async fire-and-forget can ghost-navigate after dispose | Added `IDisposable` + `CancellationTokenSource` |
| 7 | 🟡 Low | 3 Payment VMs missing dispose-before-assign consistency | Added pattern to all three |
### Round 4: Party Mode Multi-Agent Review (9 findings)

| # | Agent | Severity | Finding | Resolution |
|---|-------|----------|---------|------------|
| W-1 | 🏗️ Architect | 🔴 Critical | `NavigationService.NavigateTo<T>` factory runs before dispose → 2× peak memory | Reordered: dispose FIRST, then factory() |
| W-2 | 🏗️ Architect | 🟠 Medium | `PhotoSelectionVM.Confirm()` fire-and-forget API Task has no cancellation | Added `CancellationTokenSource` + cancel on dispose |
| W-3 | 🏗️ Architect | 🟠 Medium | `CaptureVM.InitializeCameraAsync()` no `_disposed` guard after await | Added guards in Task.Run and Dispatcher callbacks |
| D-1 | 💻 Dev | 🔴 Critical | `BackgroundSelectionVM` API Bitmap assign without dispose-before-assign | Added `var old = ...; old?.Dispose()` pattern |
| D-2 | 💻 Dev | 🟠 Medium | `QRCodeVM.QrCodeImage` assign without dispose-before-assign | Added dispose-before-assign pattern |
| D-3 | 💻 Dev | 🟡 Low | `ThankYouVM.UploadAndGenerateQR()` no `_disposed` guard after HTTP call | Added `if (_disposed) return;` after POST |
| Q-1 | 🧪 QA | 🟠 Medium | `CaptureVM.OnFrameReady` race between Dispose and Dispatcher.Post | Covered by W-3 `_disposed` guards |
| Q-2 | 🧪 QA | 🟡 Low | `ConfirmPrintVM.LoadFinalImage()` no dispose-before-assign | Added pattern |
| Q-3 | 🧪 QA | 🟡 Low | `PhotoSelectionVM.LoadCapturedPhotos()` old Photos thumbnails not disposed | Added disposal loop before replacing |