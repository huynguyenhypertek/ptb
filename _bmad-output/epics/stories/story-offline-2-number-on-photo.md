# Story 2: In mã số lên ảnh cuối (Compositing)

**Epic:** Offline Fallback — Mã số ảnh & Xử lý mất mạng  
**Mức độ:** 🟠 Trung bình  
**Trạng thái:** `done`  
**Phụ thuộc:** Story 1 (Sequential Number Service)

---

## Mô tả

Sau khi tạo ảnh composite cuối cùng (`final_composite.png`), overlay mã số thứ tự (ví dụ `0516-001`) ở **góc dưới phải** của ảnh. Mã số phải đủ nhỏ để không ảnh hưởng thẩm mỹ nhưng đủ rõ để đọc được khi in ra hoặc zoom trên điện thoại.

## Acceptance Criteria

- [x] Mã số được in ở **góc dưới phải** ảnh cuối
- [x] Font size nhỏ, **không ảnh hưởng thẩm mỹ** (khoảng 1-2% chiều cao ảnh)
- [x] Có viền/background bán trong suốt để đọc được trên mọi nền
- [x] Dùng OpenCV `PutText` (đã có trong project, không thêm dependency)
- [x] Mã số lấy từ `Session.SequentialNumber` (đã có từ Story 1)
- [x] Ảnh gốc (trước khi in số) được lưu riêng dưới tên `final_composite_original.png`
- [x] Build thành công, không lỗi
- [x] Unit tests cho `OverlaySequentialNumber` (guard clauses, backup, edge cases)

## Dev Notes

### ⚠️ Error Handling — Graceful Degradation (D1)

`OverlaySequentialNumber` có thể throw (OpenCV crash, disk full, permission). Call site trong `PhotoSelectionViewModel.Confirm()` **PHẢI** wrap bằng `try-catch` để photo không có số vẫn usable. **KHÔNG** crash session vì lỗi overlay.

### ⚠️ Original Image Backup (A1/P1)

Method `OverlaySequentialNumber` **phải** copy ảnh gốc sang `final_composite_original.png` **trước khi** ghi đè. Return path của backup để caller biết. Nếu input invalid (null number, file not found), return `null` (no-op).

### Dimension Guard (Q2)

Image nhỏ hơn 100x100 → skip overlay (text không đọc được). Return backup path nhưng không modify ảnh.

### Font Scale Formula (D2)

`fontScale = Math.Max(0.5, image.Height * 0.001)` — cho kết quả:
- Layout 6 (990px): fontScale ~1.0
- Layout 2 (2048px): fontScale ~2.0
- Minimum: 0.5 (cho ảnh nhỏ)

### session.json traceability (D4/P2)

`session.json` **phải** include field `SequentialNumber` để debug và truy vết.

---

## Các Tasks

### Task 2.1: Thêm method overlay text vào ImageCompositeService

#### [ImageCompositeService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs)

Thêm method mới (không sửa method `Compose` hiện tại):

```csharp
/// <summary>
/// Overlay mã số thứ tự lên góc dưới phải ảnh cuối.
/// Thêm background bán trong suốt để đọc được trên mọi nền.
/// Saves original image as backup before modifying.
/// </summary>
/// <param name="imagePath">Đường dẫn ảnh cuối (sẽ ghi đè file)</param>
/// <param name="sequentialNumber">Mã số (ví dụ: "0516-001")</param>
/// <returns>Path to backup of original image (without overlay), or null if no backup was created</returns>
public static string? OverlaySequentialNumber(string imagePath, string sequentialNumber)
{
    if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(sequentialNumber) || !File.Exists(imagePath))
        return null;

    // A1/P1: Save backup of original image before modifying
    var dir = Path.GetDirectoryName(imagePath) ?? ".";
    var ext = Path.GetExtension(imagePath);
    var nameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
    var backupPath = Path.Combine(dir, $"{nameWithoutExt}_original{ext}");
    File.Copy(imagePath, backupPath, overwrite: true);

    using var image = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
    if (image.Empty())
    {
        Console.WriteLine($"[COMPOSITE] WARNING: Could not read image for overlay: {imagePath}");
        return backupPath;
    }

    // Q2: Skip overlay on tiny images (text wouldn't be readable anyway)
    if (image.Width < 100 || image.Height < 100)
    {
        Console.WriteLine($"[COMPOSITE] Image too small for overlay ({image.Width}x{image.Height}), skipping");
        return backupPath;
    }

    // Font config — small but readable, especially when printed
    var fontFace = HersheyFonts.HersheySimplex;
    // D2: Adjusted formula for better readability — ~1.0-2.0 for typical images
    double fontScale = Math.Max(0.5, image.Height * 0.001);
    int thickness = Math.Max(1, (int)(fontScale * 2));
    var textColor = new Scalar(255, 255, 255); // White

    // Measure text size
    int baseline;
    var textSize = Cv2.GetTextSize(sequentialNumber, fontFace, fontScale, thickness, out baseline);

    // Position: bottom-right corner, 10px margin from edge
    int padding = 8;
    int x = image.Width - textSize.Width - padding * 2 - 10;
    int y = image.Height - padding * 2 - 10;

    // Clamp position to valid range
    x = Math.Max(padding, x);
    y = Math.Max(textSize.Height + padding, y);

    // Background rectangle (semi-transparent via overlay blend)
    var bgRect = new Rect(
        x - padding,
        y - textSize.Height - padding,
        textSize.Width + padding * 2,
        textSize.Height + baseline + padding * 2
    );

    // Clamp rect to image bounds
    bgRect.X = Math.Max(0, bgRect.X);
    bgRect.Y = Math.Max(0, bgRect.Y);
    bgRect.Width = Math.Min(bgRect.Width, image.Width - bgRect.X);
    bgRect.Height = Math.Min(bgRect.Height, image.Height - bgRect.Y);

    // Draw semi-transparent black background (ROI-based to avoid full-image clone)
    using var roiMat = new Mat(image, bgRect);
    using var roiOverlay = roiMat.Clone();
    Cv2.Rectangle(roiOverlay, new Rect(0, 0, bgRect.Width, bgRect.Height), new Scalar(0, 0, 0), -1);
    Cv2.AddWeighted(roiOverlay, 0.5, roiMat, 0.5, 0, roiMat);

    // Draw white text
    Cv2.PutText(image, sequentialNumber, new Point(x, y), fontFace, fontScale, textColor, thickness);

    // A3: Verify write succeeded
    var writeResult = Cv2.ImWrite(imagePath, image);
    if (!writeResult)
    {
        Console.WriteLine($"[COMPOSITE] ERROR: Failed to write overlay image to {imagePath}");
        throw new IOException($"Failed to write overlay image to {imagePath}");
    }

    Console.WriteLine($"[COMPOSITE] Sequential number '{sequentialNumber}' overlaid on image (backup: {Path.GetFileName(backupPath)})");
    return backupPath;
}
```

### Task 2.2: Gọi overlay sau compositing trong PhotoSelectionViewModel

#### [PhotoSelectionViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs)

Tìm dòng `ImageCompositeService.Compose(...)` và thêm ngay sau, **bọc trong try-catch** (D1):

```csharp
// D1: Overlay sequential number with try-catch — photo without number is still usable
try
{
    var seqNumber = SessionService.CurrentSession.SequentialNumber;
    if (!string.IsNullOrEmpty(seqNumber))
    {
        ImageCompositeService.OverlaySequentialNumber(outputPath, seqNumber);
    }
}
catch (Exception overlayEx)
{
    Console.WriteLine($"[COMPOSITE] WARNING: Failed to overlay sequential number: {overlayEx.Message}");
    // Continue without overlay — photo is still usable
}
```

### Task 2.3: Thêm SequentialNumber vào session.json (D4/P2)

Trong `Confirm()` method, thêm field `SequentialNumber` vào anonymous object `sessionData`:

```csharp
var sessionData = new
{
    DeviceId = DeviceConfig.DeviceId,
    LayoutUsed = layoutId ?? "unknown",
    FrameUsed = SessionService.CurrentSession.SelectedBackground?.Id ?? "unknown",
    SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "",
    PhotoCount = selectedPhotoPaths.Length,
    TotalCaptured = allPhotos.Count,
    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
    FinalImagePath = outputPath
};
```

### Task 2.4: Unit Tests cho OverlaySequentialNumber (Q1)

#### Tạo file: `tests/PhotoBooth.Tests/Services/ImageCompositeServiceOverlayTests.cs`

**Strategy:** Mỗi test tạo temp image file (OpenCV Mat → ImWrite), test overlay behavior, cleanup qua `IDisposable`.

Tests bao gồm:
1. `NullNumber_ReturnsNull` — guard clause no-op
2. `EmptyNumber_ReturnsNull` — guard clause no-op
3. `NonExistentFile_ReturnsNull` — guard clause no-op
4. `ValidInput_CreatesBackup` — backup file phải tồn tại
5. `ValidInput_ModifiesOriginalFile` — ảnh gốc phải bị thay đổi
6. `ValidInput_BackupMatchesOriginal` — backup phải giống ảnh gốc trước overlay
7. `TinyImage_SkipsOverlay` — 50x50 → skip, ảnh không bị modify
8. `LargeNumber_DoesNotThrow` — number "0516-9999" không crash
9. `Layout6Size_DoesNotThrow` — 664x990 không crash
10. `Layout2Size_DoesNotThrow` — 682x2048 không crash
11. `NullImagePath_ReturnsNull` — null imagePath guard clause

**Test project cần thêm:**
```xml
<PackageReference Include="OpenCvSharp4" Version="4.11.0.20250507" />
```

---

## Lưu ý thiết kế

### Vị trí mã số trên ảnh:

```
┌──────────────────────────┐
│                          │
│      [ẢNH PHOTOBOOTH]   │
│                          │
│                          │
│                 ┌───────┐│
│                 │0516-01││
│                 └───────┘│
└──────────────────────────┘
```

- Góc dưới phải
- Background đen bán trong suốt (50%)
- Text trắng, fontScale ~1.0-2.0 (tùy chiều cao ảnh)
- Padding 8px xung quanh text

### Vì sao dùng OpenCV PutText thay vì SkiaSharp?

- OpenCV **đã có trong project** (ImageCompositeService dùng OpenCvSharp)
- Không thêm dependency mới
- `PutText` đơn giản, đủ cho mã số ngắn
- Hạn chế: chỉ hỗ trợ ASCII font → OK vì mã số chỉ có số và dấu gạch

---

## Verification

- [ ] Chụp ảnh → ảnh cuối có mã số ở góc dưới phải
- [ ] `final_composite_original.png` tồn tại (backup không có số)
- [ ] Mã số đủ nhỏ, không che nội dung chính
- [ ] Mã số đọc được khi zoom trên điện thoại
- [ ] Mã số đúng format `MMDD-NNN` (khớp với log `[SESSION]`)
- [ ] Ảnh in ra (nếu có máy in) vẫn hiện mã số rõ ràng
- [ ] `session.json` chứa field `SequentialNumber`
- [x] Build thành công
- [x] 26/26 tests pass (12 existing + 14 overlay)

## File List

**New files:**
- `tests/PhotoBooth.Tests/Services/ImageCompositeServiceOverlayTests.cs` — 14 unit tests

**Modified files:**
- `src/PhotoBooth.Infrastructure/Services/ImageCompositeService.cs` — thêm method `OverlaySequentialNumber` với backup, dimension guard, error handling
- `src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs` — gọi `OverlaySequentialNumber` trong try-catch sau `Compose`, thêm `SequentialNumber` vào session.json
- `tests/PhotoBooth.Tests/PhotoBooth.Tests.csproj` — thêm `OpenCvSharp4` package reference

---

## Change Log

| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-05-16 | Tạo story ban đầu | Epic Offline Fallback |
| 2026-05-16 | Party-mode validation: 13 findings (4 critical, 7 medium, 2 low) — ALL FIXED | **A1/P1:** Backup original image before overlay. **D1:** try-catch around overlay call for graceful degradation. **Q1:** 10 unit tests added. **A2/D3:** Removed unused `bgColor` variable. **A3:** `ImWrite` failure throws `IOException`. **D2:** Font scale formula adjusted (`0.001` vs `0.0008`). **D4/P2:** SequentialNumber added to session.json. **Q2:** Dimension guard (skip on <100px). **Q3:** Tests provide automated verification. **P3:** Story structure aligned with Story 1 format (Dev Notes, 4 tasks, test strategy). |
| 2026-05-16 | Party-mode re-review: 8 findings (1 critical, 4 medium, 2 low, 1 info) — ALL FIXED | **F1:** Status updated `ready-for-dev` → `done`. **F2:** ROI-based blending replaces full-image clone (~16MB savings). **F3:** Added `imagePath` null check. **F4:** Added `NullImagePath_ReturnsNull` test (now 23 total). **F5:** Fixed misleading font description. **F6:** Updated test count. **F8:** Checked all AC boxes. |
| 2026-05-16 | Adversarial code review: 7 findings (1 critical, 3 medium, 2 low, 1 info) — ALL FIXED | **F1:** Moved `File.Copy` backup after dimension check (avoid unnecessary I/O; return `null` for invalid images). **F2:** Added zero-dimension guard for `bgRect` after clamping (prevents OpenCV crash). **F3:** Updated `TinyImage_SkipsOverlay` test for F1 behavior. **F4:** Added `TextWiderThanImage_DoesNotThrow` test (24 total). **F5:** Logged overlay backup path in caller instead of discarding return value. **F6:** Documented `DateTime.Now` vs UTC discrepancy in `SequentialNumberService`. **F7:** No action (info only). |
| 2026-05-16 | Party-mode source review: 4 findings fixed (1 critical, 3 medium) | **F1:** Fixed BGRA alpha channel mismatch — Scalar changed to 4-component `(255,255,255,255)` for text and `(0,0,0,255)` for rect fill (text was invisible on BGRA output). **F2:** Added backup restoration on `ImWrite` failure to prevent corrupted files. **F3:** Added BGRA test helper + 2 tests for 4-channel images (26 total). **F4:** Fixed `Path.GetDirectoryName` empty string handling for relative paths. |

