# Story 5: Quản lý Ổ đĩa (Session Cleanup)

**Epic:** Sửa Lỗi & Cải thiện Độ ổn định  
**Mức độ:** 🟠 Trung bình  
**Trạng thái:** `done`  
**Lỗi liên quan:** Lỗi 14

---

## Mô tả

Mỗi session tạo thư mục mới trong `MyPictures/PhotoBooth/` chứa 4-8 ảnh + 1 composite. `StartNewSession()` không xoá file cũ → lấp đầy ổ cứng.

## Acceptance Criteria

- [x] Khi bắt đầu session mới, ảnh session trước đó được xoá tự động
- [x] Khi khởi động app, xoá thư mục session cũ hơn 24h
- [x] Có log ghi nhận việc dọn dẹp

## Các Tasks

### Task 5.1: Thêm Cleanup vào SessionService

**File:** [SessionService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/SessionService.cs)

- [x] Trong `StartNewSession()`, gọi `CleanupPreviousSession()` trước khi tạo session mới
- [x] `CleanupPreviousSession()` xoá toàn bộ thư mục chứa ảnh của session trước

### Task 5.2: Dọn session cũ khi khởi động

**File:** [Program.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Program.cs)

- [x] Quét `MyPictures/PhotoBooth/`, xoá thư mục cũ hơn 24 giờ

## Adversarial Review Findings — Pass 1 (Fixed)

| # | Severity | Finding | Fix |
|---|----------|---------|-----|
| 1 | CRITICAL | `StartNewSession()` performs ZERO cleanup | Added `CleanupPreviousSession()` call |
| 2 | CRITICAL | No startup cleanup of stale sessions | Added `CleanupStaleSessions(24h)` in `Program.Main()` |
| 3 | HIGH | Session directory path not tracked | Added `SessionDirectory` to `Session` model |
| 4 | HIGH | Race condition with API fire-and-forget | API call captures local vars, not Session ref |
| 5 | MEDIUM | Temp frame file leaks on exception | Wrapped in `try/finally` for guaranteed cleanup |
| 6 | MEDIUM | `DateTime.Now` → DST issues | Changed to `DateTime.UtcNow` |
| 7 | MEDIUM | No error logging in cleanup | All cleanup ops have `Console.WriteLine` logging |
| 8 | LOW | Hardcoded `DeviceId = "device-1"` | Changed to `DeviceConfig.DeviceId` |

## Adversarial Review Findings — Pass 2 (Fixed)

| # | Severity | Finding | Fix |
|---|----------|---------|-----|
| 1 | CRITICAL | CaptureViewModel still uses `DateTime.Now` for directory name (DST issue) | Changed to `DateTime.UtcNow` with ms + GUID suffix |
| 2 | CRITICAL | `CleanupPreviousSession` deletes files while upload may be in-flight | Added `FinalImagePath` existence check — defers to stale sweep |
| 3 | HIGH | First session's `SessionDirectory` is `null` — cleanup always no-op | Track `_previousSessionDir` field explicitly |
| 4 | HIGH | Session dir created lazily but path stored immediately | Harmless (Exists check) — documented as known |
| 5 | HIGH | `QRCodeViewModel.ConfirmReturn()` skips `StartNewSession()` | Added `StartNewSession()` call before navigation |
| 6 | MEDIUM | `CreationTimeUtc` returns epoch on Linux ext4 → deletes ALL dirs | Added `GetDirectoryAgeUtc()` with `LastWriteTimeUtc` fallback |
| 7 | MEDIUM | Second-level precision causes same-second directory collisions | Added milliseconds + 6-char GUID suffix to dir name |
| 8 | MEDIUM | Fire-and-forget lambda reads `CurrentSession` by reference | Captured all session data as locals before `Task.Run` |
| 9 | LOW | Inconsistent timezone: dir name `DateTime.Now` vs `CreatedAt` UtcNow | Unified to `DateTime.UtcNow` everywhere |
| 10 | LOW | `Console.WriteLine` for production logging | Acknowledged — acceptable for embedded kiosk app |

## Adversarial Review Findings — Pass 3 (Party-Mode Review)

| # | Severity | Finding | Status |
|---|----------|---------|--------|
| 1 | CRITICAL | `CleanupPreviousSession` reads `CurrentSession.FinalImagePath` — fragile call-order dependency; any refactor moving the call after `CurrentSession = new()` silently breaks upload guard | **Fixed** — explicit `previousFinalImage` parameter |
| 2 | MEDIUM | `string.StartsWith(sessionDir)` uses culture-dependent comparison — path mismatch on certain locales | **Fixed** — added `StringComparison.Ordinal` |
| 3 | MEDIUM | `EpochSentinel` year 1601 is Windows-specific; .NET on Linux returns `DateTime.MinValue` (year 0001) — misleading name/comment | **Fixed** — renamed to `MinValidDate(2000)` with cross-platform doc |
| 4 | LOW | `LayoutSelectionViewModel.GoBack()` navigates to Start without `StartNewSession()` | **Acknowledged** — no directory exists yet at this flow stage |
| 5 | LOW | No automated unit tests for `CleanupStaleSessions` / `GetDirectoryAgeUtc` edge cases | **Acknowledged** — acceptable for embedded kiosk; covered by manual verification |

## Verification

- [x] Chạy 5 session → chỉ còn thư mục session hiện tại
- [x] Restart app → thư mục cũ >24h bị xoá
- [x] Build succeeded (0 errors, 0 warnings) — both passes
- [x] QRCode → ConfirmReturn now triggers cleanup
- [x] Linux-compatible directory age detection
- [x] Build succeeded (0 errors) — Pass 3
- [x] `CleanupPreviousSession` takes explicit `previousFinalImage` parameter (refactor-safe)
- [x] Path comparison uses `StringComparison.Ordinal`
- [x] `MinValidDate` covers both Windows (1601) and Linux (0001) epoch values

