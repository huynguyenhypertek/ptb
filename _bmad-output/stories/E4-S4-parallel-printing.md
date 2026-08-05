# Story 4.4: Parallel Printing

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want my photos to start printing automatically as soon as I enter the review screen,
so that I don't have to manually press a print button and the overall time spent is minimized.

## Acceptance Criteria

1. **Automatic Printing Execution**:
   - Trigger `StartPrintingAsync()` automatically during `ReviewPrintViewModel` initialization (print immediately upon entering the screen, no manual confirmation required).
   - `StartPrintingAsync()` MUST utilize the injected `IPrintService` from `PhotoBooth.Infrastructure` by calling `IPrintService.PrintImageAsync()`.
   - Wait for `GenerateQROverlayAsync()` to completely finish (success or failure) BEFORE triggering the print, so the printed photo includes the generated QR code.

2. **UI & State Management**:
   - Keep `StartPrintingAsync` as a `[RelayCommand]` so it can still be bound to a UI "Thử lại" (Retry) button for manual retry if the automatic print fails.
   - Prevent double-printing: Introduce an `_isPrinting` flag to `StartPrintingAsync()` so rapid manual clicks don't queue multiple print jobs while one is active.
   - Update `StatusText` and `Progress` on the UI thread when printing starts/ends (e.g., "Đang gửi lệnh in...", "⚠️ Lỗi máy in").

3. **Fault Tolerance**:
   - If QR generation fails (e.g., offline mode), the automatic print MUST still execute (printing the image without the QR code).

## Tasks / Subtasks

- [x] Task 1: Refactor `ReviewPrintViewModel` initialization to safely chain automatic printing.
- [x] Task 2: Update `StartPrintingAsync` to include an `_isPrinting` guard.
- [x] Task 3: Ensure `StatusText` updates from background tasks are dispatched to the UI thread if necessary.
- [x] Task 4: Update `ReviewPrintViewModelTests` to verify automatic printing behavior and retry logic.

## Dev Agent Record
- **Completion Notes**: Implemented automatic printing immediately after QR generation completes by refactoring the constructor to use `InitializeAndPrintAsync()`. Added an `_isPrinting` guard to `StartPrintingAsync` to prevent rapid double-printing if triggered manually by a retry button. Updated the `StatusText` updates to ensure thread safety using `Dispatcher.UIThread.Post()` with a fallback for unit tests where Avalonia might not be fully initialized. Unit tests were updated and pass.
- **Code Review (AI)**: Fixed race condition between LoadFinalImageAsync and StartPrintingAsync. Also fixed test flakiness and added print service return value handling.

## File List
- `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs` (MODIFIED)
- `tests/PhotoBooth.Tests/ViewModels/ReviewPrintViewModelTests.cs` (MODIFIED)

## Change Log
- Refactored `ReviewPrintViewModel` to automatically print immediately after QR generation by calling `InitializeAndPrintAsync()`. Added `_isPrinting` lock and UI thread dispatching. Updated unit tests.

## Dev Notes

**DO THIS:**
- Inject `IPrintService` (from `PhotoBooth.Infrastructure`) into `ReviewPrintViewModel`.
- Ensure `StartPrintingAsync()` is called in the `finally` block or at the end of `GenerateQROverlayAsync()`.
- Use `Dispatcher.UIThread.InvokeAsync(...)` if you update `StatusText` or `Progress` from a background thread inside `GenerateQROverlayAsync()`.
- Keep the `[RelayCommand]` attribute on `StartPrintingAsync`.

**DO NOT DO THIS:**
- DO NOT use `Task.WhenAll` to run QR overlay and printing in parallel. `ImageCompositeService.OverlayQrCode` writes to the file. `PrintService.PrintImageAsync` reads the file. Parallel execution will cause `IOException: The process cannot access the file because it is being used by another process`.
- DO NOT let `StartPrintingAsync()` execute before `LoadFinalImageAsync()` completes its stream copy. Ensure `FileShare.Read` is respected.

### Project Structure Notes

- Files to modify:
  - `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs`
  - `tests/PhotoBooth.Tests/ViewModels/ReviewPrintViewModelTests.cs`
