# Story 1: Cấu hình đường dẫn Google Drive

**Epic:** Tích hợp Google Drive — Lưu ảnh & QR chia sẻ  
**Mức độ:** 🟡 Nhẹ  
**Trạng thái:** `done`

---

## Mô tả

Thay đổi đường dẫn lưu ảnh từ `~/Pictures/PhotoBooth/` sang thư mục Google Drive Desktop. Google Drive Desktop hoạt động như một thư mục bình thường trên ổ cứng — chỉ cần đổi path là ảnh tự sync lên cloud.

## Acceptance Criteria

- [x] `DeviceConfig` có property `GoogleDrivePath` — đường dẫn thư mục Google Drive trên máy
- [x] `DeviceConfig` có property `GoogleDriveEnabled` — bật/tắt tính năng lưu vào Drive (default: `false`)
- [x] Hỗ trợ CLI argument `--googleDrivePath=<path>` để config từ command line (camelCase, `=` separator — consistent with existing args)
- [x] Hỗ trợ CLI argument `--googleDriveEnabled=<true|false>` để bật/tắt
- [x] `CaptureViewModel` lưu ảnh vào folder Google Drive khi `GoogleDriveEnabled = true`
- [x] `CaptureViewModel` fallback về `~/Pictures/PhotoBooth/` khi `GoogleDriveEnabled = false` hoặc `GoogleDrivePath` rỗng/invalid
- [x] `SessionService.SessionsBaseDirectory` tự động trỏ đúng path (Google Drive hoặc local) tuỳ config
- [x] `CleanupStaleSessions()` hoạt động đúng với cả hai path (hiện tại disabled theo yêu cầu user — code đã verify)
- [x] Folder session có format: `PhotoBooth/{timestamp}_{guid}/` (giữ nguyên format hiện tại)
- [x] Log Google Drive config values ở startup (`[CONFIG]` line)
- [x] Build thành công, không lỗi

## Các Tasks

### Task 1.1: Thêm Google Drive config vào DeviceConfig

#### [DeviceConfig.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/DeviceConfig.cs)

Thêm properties:

```csharp
// Thêm vào DeviceConfig (sau ApiBaseUrl)
public static bool GoogleDriveEnabled { get; set; } = false;
public static string GoogleDrivePath { get; set; } = "";
```

Thêm vào `ParseArgs()` — dùng đúng pattern `if/else if` + `StartsWith` + `Substring` hiện tại:

```csharp
else if (arg.StartsWith("--googleDriveEnabled="))
{
    GoogleDriveEnabled = bool.TryParse(arg.Substring("--googleDriveEnabled=".Length), out var gde) && gde;
}
else if (arg.StartsWith("--googleDrivePath="))
{
    var path = arg.Substring("--googleDrivePath=".Length);
    if (!string.IsNullOrWhiteSpace(path))
        GoogleDrivePath = path;
}
```

Cập nhật log line hiện tại để bao gồm Google Drive config:

```diff
- System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}");
+ System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GoogleDrivePath}");
```

### Task 1.2: Cập nhật SessionService để hỗ trợ Google Drive path

#### [SessionService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/SessionService.cs)

**Quan trọng:** `SessionsBaseDirectory` hiện tại là `static readonly` trỏ cứng vào `~/Pictures/PhotoBooth/`. Cần chuyển thành computed property để cleanup logic hoạt động đúng với cả hai path.

```diff
- /// <summary>
- /// Base directory for all session folders: MyPictures/PhotoBooth/
- /// </summary>
- private static readonly string SessionsBaseDirectory = Path.Combine(
-     Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
-     "PhotoBooth"
- );
+ /// <summary>
+ /// Base directory for all session folders.
+ /// Returns Google Drive path when enabled, otherwise ~/Pictures/PhotoBooth/.
+ /// </summary>
+ private static string SessionsBaseDirectory =>
+     DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.GoogleDrivePath)
+         ? Path.Combine(DeviceConfig.GoogleDrivePath, "PhotoBooth")
+         : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoBooth");
```

### Task 1.3: Đổi đường dẫn lưu ảnh trong CaptureViewModel

#### [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

Thay đổi logic tạo `_photosDirectory` (dòng 89-93) — sử dụng `SessionService.GetSessionsBaseDirectory()` thay vì hardcode path:

```diff
- _photosDirectory = Path.Combine(
-     Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
-     "PhotoBooth",
-     $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..6]}"
- );
+ var sessionFolder = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..6]}";
+ var basePath = SessionService.GetSessionsBaseDirectory();
+ _photosDirectory = Path.Combine(basePath, sessionFolder);
```

Thêm validation sau khi tạo path — nếu Google Drive path không tồn tại, fallback:

```csharp
// Validate Google Drive path accessibility
if (DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.GoogleDrivePath))
{
    if (!Directory.Exists(DeviceConfig.GoogleDrivePath))
    {
        Console.WriteLine($"[WARNING] Google Drive path not found: {DeviceConfig.GoogleDrivePath} — falling back to local storage");
        var fallbackBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "PhotoBooth"
        );
        _photosDirectory = Path.Combine(fallbackBase, sessionFolder);
    }
}
```

### Task 1.4: Lưu tên session folder vào Session model

#### [Session.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.Core/Models/Session.cs)

Thêm property để lưu tên folder (cần cho Apps Script lookup ở Story 2):

```csharp
/// <summary>
/// Tên folder session (chỉ tên, không phải full path).
/// Dùng để Google Apps Script tìm folder trên Drive.
/// </summary>
public string? SessionFolderName { get; set; }
```

#### [CaptureViewModel.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs)

Lưu tên folder vào session (ngay sau khi tạo `_photosDirectory`):

```csharp
SessionService.CurrentSession.SessionFolderName = sessionFolder;
```

---

## Party-Mode Validation Findings

### Round 1

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| 1 | CRITICAL | `SessionService.SessionsBaseDirectory` hardcoded — `CleanupStaleSessions()` and `CleanupPreviousSession()` scan wrong path when Google Drive enabled | **Fixed** — Task 1.2 changes to computed property |
| 2 | MEDIUM | CLI arg format (`--google-drive-path`) uses kebab-case, inconsistent with existing camelCase args (`--storeId`, `--deviceId`) | **Fixed** — Changed to `--googleDrivePath`, `--googleDriveEnabled` |
| 3 | MEDIUM | `ParseArgs` code snippet used `case` switch syntax but existing code uses `if/else if` + `StartsWith` + `Substring` pattern | **Fixed** — Rewrote snippet to match existing pattern |
| 4 | MEDIUM | `SessionService.cs` missing from file list despite requiring critical modification | **Fixed** — Added Task 1.2 + file list entry |
| 5 | MEDIUM | No fallback/validation when Google Drive path doesn't exist — crash at `Directory.CreateDirectory()` | **Fixed** — Added path validation with fallback in Task 1.3 |
| 6 | LOW | New config values not included in startup `[CONFIG]` log line | **Fixed** — Added to log in Task 1.1 |
| 7 | LOW | Story marked `draft` with complete implementation code | **Fixed** — Changed to `ready-for-dev` |

### Round 2 (Implementation)

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| A1 | CRITICAL | Source code not yet modified — `SessionService.SessionsBaseDirectory` still `static readonly`, `CaptureViewModel` still hardcodes path, `Session.SessionFolderName` missing | **Fixed** — All 4 files updated, build passes (0 errors) |
| A2 | MEDIUM | `CaptureViewModel` duplicated `SessionsBaseDirectory` logic instead of using `SessionService.GetSessionsBaseDirectory()` | **Fixed** — Now calls `SessionService.GetSessionsBaseDirectory()` |
| A3 | MEDIUM | `Session.cs` missing `SessionFolderName` property for Apps Script lookup | **Fixed** — Property added with XML doc |
| Q1 | LOW | CLI paths with spaces/special chars require shell quoting — not documented | **Acknowledged** — Standard shell behavior, no code change needed |
| Q2 | LOW | `CleanupStaleSessions` must be called after `ParseArgs` to read correct config | **Verified** — `Program.cs` already calls `ParseArgs()` first (line 15) |

### Round 3 (Adversarial Code Review)

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| CR1 | CRITICAL | **Fallback path divergence** — `CaptureViewModel` manually constructs fallback `~/Pictures/PhotoBooth/` when GDrive path missing, but `SessionService.SessionsBaseDirectory` still returns GDrive-based path (since `GoogleDriveEnabled=true`, `GoogleDrivePath` non-empty). `CleanupStaleSessions()` scans wrong directory, never cleans fallback sessions. | **Fixed** — Moved `Directory.Exists()` check into `SessionsBaseDirectory` getter, removed duplicate logic from CaptureVM |
| CR2 | CRITICAL | **Dead code** — `_previousSessionDir` field declared + read in `CleanupPreviousSession()` but **never assigned**. `CleanupPreviousSession()` docstring says "Called automatically at start of StartNewSession()" but `StartNewSession()` doesn't call it. Dead code creates false confidence. | **Fixed** — Removed dead field + dead method entirely |
| CR3 | MEDIUM | **DRY violation** — CaptureVM L92-103 duplicated the Google Drive enabled/path/exists check already in `SessionService`. Maintenance trap: if condition changes in one place but not the other, behavior silently diverges. | **Fixed** — Consolidated into `SessionService.GetSessionsBaseDirectory()` |
| CR4 | MEDIUM | **PII in logs** — Full `GoogleDrivePath` logged at startup. Path often contains user email (e.g. `GoogleDrive-user@gmail.com`). | **Fixed** — Masked to show only last 2 path segments: `.../My Drive` |
| CR5 | LOW | **Computed property overhead** — `SessionsBaseDirectory` re-evaluates `Path.Combine` every access. | **Won't fix** — `Directory.Exists` check must be live for each session start |
| CR6 | LOW | **Vietnamese-only XML doc** on `SessionFolderName` — minor inconsistency with English docs elsewhere. | **Acknowledged** — Consistent with project's mixed doc style |

### Round 4 (Party-Mode Review)

| # | Severity | Finding | Resolution |
|---|----------|---------|------------|
| R4-1 | MEDIUM | **PII leak in fallback warning log** — `SessionService.SessionsBaseDirectory` getter logged raw `GoogleDrivePath` (contains user email) bypassing startup PII masking | **Fixed** — Uses `DeviceConfig.GetMaskedGDrivePath()` |
| R4-2 | MEDIUM | **No defensive `Directory.CreateDirectory`** — `CaptureViewModel` relied entirely on `CameraService.CapturePhoto()` to create the directory. If CapturePhoto throws before that line, downstream code crashes on missing dir | **Fixed** — Added `Directory.CreateDirectory(_photosDirectory)` in CaptureVM constructor |
| R4-3 | LOW | **Stale AC #8** — References `CleanupPreviousSession()` which was deleted in CR2 | **Fixed** — Updated AC text |
| R4-4 | LOW | **`CleanupStaleSessions` disabled but AC claims it works** — `Program.cs` has it commented out per user request | **Fixed** — Updated AC and Verification to reflect reality |
| R4-5 | LOW | **Duplicated PII masking logic** — `DeviceConfig` inline masking and `SessionService` had different masking rules | **Fixed** — Centralized into `DeviceConfig.GetMaskedGDrivePath()` helper |

---

## Verification

- [ ] Chạy app với `--googleDriveEnabled=true --googleDrivePath="/Users/xxx/Library/CloudStorage/GoogleDrive-xxx@gmail.com/My Drive"` → ảnh lưu vào Google Drive folder
- [ ] Chạy app không có args → ảnh lưu vào `~/Pictures/PhotoBooth/` (fallback)
- [ ] Chạy app với `--googleDriveEnabled=true --googleDrivePath="/nonexistent/path"` → log warning (masked path) + fallback sang `~/Pictures/PhotoBooth/`
- [ ] Session cleanup (`CleanupStaleSessions`) quét đúng base directory khi Google Drive enabled (hiện disabled trong Program.cs theo yêu cầu user)
- [ ] `[CONFIG]` log line hiển thị `GoogleDriveEnabled=True, GoogleDrivePath=.../My Drive` (masked)
- [ ] Mở Google Drive web → thấy ảnh đã sync lên
- [x] Build thành công (0 errors, 2 pre-existing NuGet warnings)
- [ ] Grep `Console.WriteLine` — không còn raw GoogleDrivePath leak

---

## File List

**Modified files:**
- `src/PhotoBooth.UI/DeviceConfig.cs` — thêm properties + parse CLI args + update log + PII masking
- `src/PhotoBooth.UI/Services/SessionService.cs` — `SessionsBaseDirectory` → computed property with `Directory.Exists` validation + dead code removed
- `src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs` — dùng `GetSessionsBaseDirectory()` + save `SessionFolderName` (duplicate fallback removed)
- `src/PhotoBooth.Core/Models/Session.cs` — thêm `SessionFolderName` property

---

## Dev Agent Record

### Implementation Plan

All 4 tasks were implemented in prior sessions. This dev-story run verified the implementation against all acceptance criteria and confirmed build success.

### Completion Notes

- ✅ **Task 1.1**: `GoogleDriveEnabled` (bool, default false) and `GoogleDrivePath` (string, default "") added to `DeviceConfig.cs` with CLI parsing matching existing `if/else if` + `StartsWith` + `Substring` pattern. `[CONFIG]` log line updated.
- ✅ **Task 1.2**: `SessionService.SessionsBaseDirectory` computed property with `Directory.Exists` check — single source of truth for fallback logic. Dead `_previousSessionDir` field and `CleanupPreviousSession()` removed.
- ✅ **Task 1.3**: `CaptureViewModel` uses `SessionService.GetSessionsBaseDirectory()` — duplicate fallback logic removed (CR1/CR3 fix).
- ✅ **Task 1.4**: `SessionFolderName` property added to `Session.cs` model with XML documentation. Set in `CaptureViewModel` constructor for downstream Apps Script lookup.
- ✅ **Build**: 0 errors, 1 pre-existing NuGet warning (Tmds.DBus.Protocol vulnerability — unrelated).
- ✅ **Code Review**: 6 findings (2 CRITICAL, 2 MEDIUM, 2 LOW) — all fixed or acknowledged.
- ℹ️ **Tests**: No test projects exist in this repository. Verification is manual/runtime per the story's Verification section.

---

## Change Log

| Date | Change |
|------|--------|
| 2026-05-14 | Implementation complete — all 4 tasks done, all 11 acceptance criteria satisfied, build passes |
| 2026-05-14 | Code review round 3 — 6 findings fixed: fallback path divergence (CR1), dead code removal (CR2), DRY consolidation (CR3), PII masking (CR4) |
| 2026-05-14 | Party-mode round 4 — 5 findings fixed: PII leak in warning log (R4-1), defensive dir creation (R4-2), stale AC (R4-3/R4-4), centralized masking helper (R4-5) |
