# Story 3.1: photo-select-viewmodel

Status: done

## Story

As a developer,
I want to implement the PhotoSelectViewModel for the Event app,
so that users can select 4 of 6 captured photos for the final composite.

## Acceptance Criteria

1. Replace the placeholder `PhotoSelectViewModel` in `src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs` with full selection logic.
2. Load all 6 captured photo paths from `SessionService.CurrentSession.CapturedPhotoPaths` and display as `PhotoItem` thumbnails.
3. Allow users to toggle selection on each photo — max 4 selections (`RequiredSelections = 4`, hardcoded).
4. Track selection order via `_selectionOrder` list so photos appear in the frame in the order the user tapped them.
5. Expose `SelectedCount` computed property for UI binding (enables/disables Confirm button when `SelectedCount == RequiredSelections`).
6. `Confirm` command: save selected indices to `SessionService.SetSelectedPhotos()`, then navigate to `ReviewPrintViewModel`.
7. `GoBack` command: navigate back to `CaptureViewModel`.
8. Implement `IDisposable` to dispose all `Bitmap` thumbnails and null out references (prevent memory leaks).
9. Do **NOT** include sticker logic, API upload fire-and-forget, QR prefetch, `ImageCompositeService` compositing, or layout-branching code.

## Tasks / Subtasks

- [x] Task 1: PhotoItem model (AC: #2, #4)
  - [x] Define `PhotoItem` class: `Index`, `Path`, `Thumbnail` (Bitmap?), `IsSelected` (ObservableProperty).
  - [x] Place in same file or a separate `Models/PhotoItem.cs` — same file preferred (matches source pattern).

- [x] Task 2: Core ViewModel properties (AC: #2, #3, #5)
  - [x] `ObservableCollection<PhotoItem> Photos`
  - [x] `int RequiredSelections = 4` (hardcoded, no layout branching)
  - [x] `int SelectedCount => Photos.Count(p => p.IsSelected)` (computed)
  - [x] `bool CanConfirm => SelectedCount == RequiredSelections` (for CanExecute binding)
  - [x] `Bitmap? SelectedPhoto1..4` for frame preview slots (4 only, NOT 6)
  - [x] `const int ThumbnailWidth = 300` — decode width for memory-efficient thumbnails (replaces source's `PhotoWidth` which is dropped)
  - [x] `bool _disposed` flag for Dispose guard

- [x] Task 3: LoadCapturedPhotos (AC: #2)
  - [x] Read paths from `_sessionService.CurrentSession.CapturedPhotoPaths`
  - [x] Create `PhotoItem` for each with `LoadThumbnail(path)` helper
  - [x] `LoadThumbnail`: use `Bitmap.DecodeToWidth(stream, ThumbnailWidth)` — uses the 300px constant defined in Task 2 (~0.5MB vs ~8MB per image)

- [x] Task 4: ToggleSelection command (AC: #3, #4)
  - [x] If already selected → deselect + remove from `_selectionOrder`
  - [x] If not selected AND `SelectedCount < RequiredSelections` → select + add to `_selectionOrder`
  - [x] Call `OnPropertyChanged(nameof(SelectedCount))` after toggle
  - [x] Call `OnPropertyChanged(nameof(CanConfirm))` after toggle
  - [x] Call `ConfirmCommand.NotifyCanExecuteChanged()` to update button state
  - [x] Call `UpdatePreviewPhotos()` to refresh SelectedPhoto1..4

- [x] Task 5: UpdatePreviewPhotos (AC: #4)
  - [x] Map `_selectionOrder[0..3]` → `SelectedPhoto1..4` (use Thumbnail reference, NOT new Bitmap)
  - [x] Set remaining slots to null

- [x] Task 6: Confirm command — sync void, NOT async Task (AC: #6, #9)
  - [x] `[RelayCommand(CanExecute = nameof(CanConfirm))]` — prevents confirm with < 4 selections
  - [x] Extract `selectedIndices` from `_selectionOrder.Select(p => p.Index).ToList()`
  - [x] Call `_sessionService.SetSelectedPhotos(selectedIndices)`
  - [x] Navigate: `_navigationService.NavigateTo<ReviewPrintViewModel>()`
  - [x] ⚠️ Method signature: `private void Confirm()` — NOT `async Task`. No async ops remain after stripping compositing + API upload.
  - [x] ⚠️ Do NOT call `ImageCompositeService.Compose()` — compositing belongs to E3-S3
  - [x] ⚠️ Do NOT fire-and-forget API upload — that was PhotoBooth.UI-specific

- [x] Task 7: GoBack command (AC: #7)
  - [x] Navigate: `_navigationService.NavigateTo<CaptureViewModel>()`

- [x] Task 8: IDisposable (AC: #8)
  - [x] Guard: `if (_disposed) return;`
  - [x] Set `_disposed = true`
  - [x] Dispose all `PhotoItem.Thumbnail` bitmaps in Photos collection
  - [x] Null out `SelectedPhoto1..4` (they reference already-disposed thumbnails — do NOT double-dispose)
  - [x] Call `GC.SuppressFinalize(this)`

## Dev Notes

- **Reuse Approach**: `copy_and_simplify` — from `PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs`

### Required Using Directives

```csharp
using System;
using System.Collections.Generic;       // List<PhotoItem>
using System.Collections.ObjectModel;    // ObservableCollection
using System.IO;                         // File.Exists, File.OpenRead
using System.Linq;                       // .Count(), .Select()
using Avalonia.Media.Imaging;            // Bitmap, Bitmap.DecodeToWidth
using CommunityToolkit.Mvvm.ComponentModel; // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;       // [RelayCommand]
using PhotoBooth.Event.Services;         // NavigationService, SessionService
```

### KEEP vs DROP vs CHANGE Guide

> [!IMPORTANT]
> When copying from the source `PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs`, strictly follow this guide.

**KEEP (adapt namespace to `PhotoBooth.Event.ViewModels`):**
- `PhotoItem` class with `Index`, `Path`, `Thumbnail`, `IsSelected` ([ObservableProperty])
- `ToggleSelection(PhotoItem)` relay command — exact same logic
- `_selectionOrder` list for tracking tap order
- `UpdatePreviewPhotos()` — but only 4 slots (not 6)
- `LoadCapturedPhotos()` — read from `SessionService.CurrentSession.CapturedPhotoPaths`
- `LoadThumbnail(string path)` — `Bitmap.DecodeToWidth` for memory efficiency
- `Dispose()` — bitmap cleanup pattern
- `SelectedCount` computed property

**DROP COMPLETELY — DO NOT COPY:**
- `LoadBackground()` — Event has no per-layout background images
- `LoadFramePreview()` — no frame preview on selection screen
- `LoadFrameFromApiAsync()` — no API frame download
- `BackgroundImage`, `FramePreviewImage` observable properties
- `PhotoWidth`, `PhotoHeight`, `PhotoMargin`, `GridMaxWidth`, `GridMargin` — layout sizing is UI/XAML concern, not ViewModel
- `IsLayout6`, `IsLayout2` — Event is single-layout (layout6 hardcoded in CaptureViewModel)
- `SelectedPhoto5`, `SelectedPhoto6` — Event selects 4, not 6
- All `ImageCompositeService.Compose()` logic in `Confirm()` — compositing moves to E3-S3
- All API fire-and-forget upload logic in `Confirm()` — not needed for Event
- `HttpService`, `DeviceConfig`, `AssetLoader` references
- All frame download/fallback logic
- All `bgId`, `layoutId` branching

**CHANGE:**
- `RequiredSelections` = **4** (hardcoded constant, not dynamic from layout)
- `Confirm()` → navigate to `ReviewPrintViewModel` (not `ConfirmPrintViewModel`)
- `GoBack()` → navigate to `CaptureViewModel` (same as source)
- Namespace: `PhotoBooth.Event.ViewModels` (not `PhotoBooth.UI.ViewModels`)
- Constructor: takes `NavigationService` + `SessionService` from `PhotoBooth.Event.Services`
- Base class: `ViewModelBase` from `PhotoBooth.Event.ViewModels` (no `NavigationService`/`SessionService` params in base — Event's ViewModelBase is parameterless)

### Architectural Constraints

- **Constructor pattern**: `PhotoSelectViewModel(NavigationService, SessionService)` — same as `StartViewModel`. Store in private readonly fields. Do NOT use base class constructor params (Event's `ViewModelBase` has no constructor parameters, unlike the UI version).
- **DI registration**: Already registered in `MainWindowViewModel` line 40: `NavigationService.RegisterViewModel(() => new PhotoSelectViewModel(NavigationService, SessionService))`. No changes needed.
- **Navigation targets**: `ReviewPrintViewModel` (forward) and `CaptureViewModel` (back) are both registered placeholders. Navigation will work.
- **No Infrastructure modifications**: Do NOT modify any file in `PhotoBooth.Infrastructure`. Only reference services.
- **Bitmap disposal**: Follow same pattern as `CaptureViewModel.Dispose()` — dispose bitmaps, null references. `SelectedPhoto1..4` point to same `Thumbnail` objects, so null them out (don't double-dispose). No `CancellationTokenSource` needed — all operations are synchronous.
- **Thread safety**: `LoadThumbnail` is sync I/O. This is acceptable because it runs in the constructor (same as source). Thumbnails are small (~0.5MB each × 6 = ~3MB total).
- **Edge case — partial capture**: If user hit Skip in CaptureVM, `CapturedPhotoPaths` may have < 6 entries. Selection logic still works correctly — user simply cannot reach 4 selections from fewer photos. `CanConfirm` stays false, Confirm button stays disabled. This is intentional; no special handling needed.

### Project Structure Notes

- **Target file**: `src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs` — overwrite the 20-line placeholder
- **No new files needed**: `PhotoItem` class goes in the same file (bottom of file, same pattern as source)
- **No new NuGet packages**: uses existing `CommunityToolkit.Mvvm`, `Avalonia.Media.Imaging`

### References

- Source: [PhotoSelectionViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs) — full source (492 lines → target ~120 lines after stripping)
- Source ToggleSelection: [PhotoSelectionViewModel.cs#L274-L289](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs#L274-L289)
- Source LoadCapturedPhotos: [PhotoSelectionViewModel.cs#L221-L242](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs#L221-L242)
- Source LoadThumbnail: [PhotoSelectionViewModel.cs#L249-L269](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs#L249-L269)
- Source PhotoItem: [PhotoSelectionViewModel.cs#L483-L491](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs#L483-L491)
- Source Dispose: [PhotoSelectionViewModel.cs#L457-L480](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs#L457-L480)
- Target placeholder: [PhotoSelectViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs)
- Session model: [Session.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Core/Models/Session.cs) — `CapturedPhotoPaths`, `SelectedPhotoIndices`
- SessionService: [SessionService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/SessionService.cs) — `SetSelectedPhotos()`, `CurrentSession`
- NavigationService: [NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs) — `NavigateTo<T>()`
- DI registration: [MainWindowViewModel.cs#L40](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L40)
- Event ViewModelBase: [ViewModelBase.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/ViewModelBase.cs) — parameterless abstract class

### Previous Story Intelligence (Epic 2)

- **Pattern established**: Event ViewModels store `_navigationService` and `_sessionService` as private readonly fields (not via base class).
- **Dispose pattern**: CaptureViewModel uses `_disposed` flag, cancels CTS, disposes bitmaps, calls `GC.SuppressFinalize(this)`. Follow same pattern.
- **Session data flow**: `CaptureViewModel` adds photos via `_sessionService.AddCapturedPhoto(path)`. This story reads them via `_sessionService.CurrentSession.CapturedPhotoPaths`.
- **Navigation chain**: StartVM → CaptureVM → **PhotoSelectVM** → ReviewPrintVM. CaptureVM already navigates to `PhotoSelectViewModel` on completion (line 300).
- **Thumbnail width**: Source uses `PhotoWidth` (253px). Since Event drops layout-specific sizing, use a hardcoded constant (e.g., `const int ThumbnailWidth = 300`).

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

None — clean implementation with no debug issues.

### Completion Notes List

- ✅ Implemented PhotoSelectViewModel by porting from PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs (492 lines → ~200 lines)
- ✅ KEPT: selection logic, MaxSelection=4, toggle, selection order tracking, thumbnail loading, dispose pattern, PhotoItem model
- ✅ DROPPED: sticker, API upload fire-and-forget, QR prefetch, layout branching, frame download, ImageCompositeService, background/frame images, layout sizing properties, SelectedPhoto5/6
- ✅ CHANGED: RequiredSelections=4 (const, not dynamic), navigate to ReviewPrintViewModel (not ConfirmPrintViewModel), sync Confirm() (not async Task), parameterless ViewModelBase, ThumbnailWidth=300 (not PhotoWidth)
- ✅ Constructor pattern follows established Event convention: private readonly fields for NavigationService + SessionService
- ✅ Dispose pattern follows CaptureViewModel: _disposed guard, GC.SuppressFinalize, null out preview slots (don't double-dispose)
- ✅ 22 unit tests covering all tasks and edge cases — all passing
- ✅ Full regression suite (38 tests) — all passing, no regressions
- ✅ Build succeeds with 0 errors

### File List

- `src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs` — modified (overwritten placeholder with full implementation)
- `tests/PhotoBooth.Tests/ViewModels/PhotoSelectViewModelTests.cs` — new (22 unit tests)

## Senior Developer Review (AI)

**Reviewer:** Nguyenhuy | **Date:** 2026-08-05 | **Outcome:** ✅ Approved (after fixes)

### Issues Found & Fixed

| ID | Severity | Description | Status |
|----|----------|-------------|--------|
| M1 | MEDIUM | `ToggleSelection` null parameter guard missing | ✅ Fixed |
| M2 | MEDIUM | Sync I/O in constructor blocks UI thread (inherited from source — accepted) | ⏭️ Deferred |
| M3 | MEDIUM | `PhotoItem.Thumbnail` not `[ObservableProperty]` — latent bug for async loading | ✅ Fixed |
| M4 | MEDIUM | `_selectionOrder` not cleared in `Dispose()` | ✅ Fixed |
| L1 | LOW | `PhotoItem` class is `public` (may be required by source generators) | ⏭️ Deferred |
| L2 | LOW | Tests use hardcoded `/tmp/` paths | ✅ Fixed |

### Fixes Applied

- **M1**: Added `if (photo is null) return;` guard at top of `ToggleSelection`
- **M3**: Changed `PhotoItem.Thumbnail` from plain auto-property to `[ObservableProperty]` backing field
- **M4**: Added `_selectionOrder.Clear()` in `Dispose()` between thumbnail disposal and preview nulling
- **L2**: Replaced `/tmp/fake_photo_*.jpg` with `Path.Combine(Path.GetTempPath(), ...)` in tests + assertions

### Deferred Items

- **M2**: Constructor sync I/O is inherited from source and explicitly accepted in story Dev Notes. Deferring to future optimization story.
- **L1**: `public` visibility may be required by CommunityToolkit source generators for XAML binding. Low risk.

## Change Log

- 2026-08-05: Implemented PhotoSelectViewModel (E3-S1) — copy_and_simplify from PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs. All 8 tasks complete, 22 tests added.
- 2026-08-05: Code review fixes (M1, M3, M4, L2) — null guard, ObservableProperty for Thumbnail, clear selection order in Dispose, cross-platform test paths.
