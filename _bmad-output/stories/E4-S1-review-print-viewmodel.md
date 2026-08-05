# Story 4.1: Review Print ViewModel

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want a single review screen that shows my final photo, handles printing, and provides a way to finish my session,
so that I don't have to navigate through multiple confirmation and thank-you screens.

## Acceptance Criteria

1. Create `ReviewPrintViewModel` in `src/PhotoBooth.Event/ViewModels/`.
2. The ViewModel must inherit from `ViewModelBase` and implement `IDisposable`.
3. **Dependency Injection:** The constructor must take `NavigationService`, `SessionService`, and `IPrintService`.
4. **State Management (Consolidate & Keep):**
   - `FinalImage` (Bitmap) to display the composite photo.
   - `PrintCopies` (int) with default 1, and `IncreaseCopies`/`DecreaseCopies` commands (bounds 1-10).
   - `StatusText` and `Progress` to track background printing/upload status.
   - `IsOffline` (bool) and `SequentialNumber` (string) for offline fallback support.
5. **Architectural Drop (Do NOT include):**
   - Drop all separate QR UI states (`QrCodeImage`, `ShowQRScreen`, `ShowThankYou`, `ShowConfirmation`). The QR code will be embedded directly onto the photo in E4-S3, so the UI no longer manages QR display states.
6. **Background Task Management:**
   - Instantiate a `CancellationTokenSource` (`_cts`) to pass to async placeholder tasks.
   - Implement `IDisposable.Dispose()` to call `_cts.Cancel()`, `_cts.Dispose()`, and properly dispose of the `FinalImage` bitmap to prevent memory leaks.
   - When assigning a new `Bitmap` to `FinalImage`, always dispose of the previous instance first (e.g., `oldImage?.Dispose()`).
7. **Action Commands:**
   - Provide a `ReturnToStartCommand` to return to `StartViewModel` by calling `_sessionService.StartNewSession()` and `_navigationService.NavigateTo<StartViewModel>()`.
8. **Asynchronous Placeholders:**
   - Include placeholder methods for `StartPrintingAsync(CancellationToken ct)` and `GenerateQROverlayAsync(CancellationToken ct)`. Call these in the constructor asynchronously without awaiting (fire-and-forget).
   - **Exception Handling:** Wrap the contents of these fire-and-forget methods in `try/catch` blocks and log any exceptions to prevent unobserved task exceptions from crashing the application.
9. **Final Image Loading:**
   - Load the final image from `SessionService.CurrentSession.FinalImagePath` into `FinalImage` during initialization.
10. Register `ReviewPrintViewModel` in `MainWindowViewModel.cs` or DI container.

## Tasks / Subtasks

- [x] Task 1: Create `ReviewPrintViewModel.cs` structure
  - [x] Inherit from `ViewModelBase` and `IDisposable`
  - [x] Inject `NavigationService`, `SessionService`, `IPrintService`
  - [x] Add `CancellationTokenSource _cts`
- [x] Task 2: Implement State Properties and Commands
  - [x] Add `[ObservableProperty]` for `FinalImage`, `PrintCopies`, `StatusText`, `Progress`, `IsOffline`, `SequentialNumber`
  - [x] Add `IncreaseCopies()` and `DecreaseCopies()` commands
  - [x] Add `ReturnToStart()` command (restarts session and navigates to Start)
- [x] Task 3: Implement Resource Management & Memory Leak Prevention
  - [x] Implement `Dispose()` method: cancel/dispose `_cts`, and `FinalImage?.Dispose()`
  - [x] Add `LoadFinalImage()` method reading from `_sessionService.CurrentSession.FinalImagePath` (ensure old bitmap is disposed before assignment)
- [x] Task 4: Setup Background Async Placeholders
  - [x] Add `StartPrintingAsync(CancellationToken ct)` placeholder with try-catch logging
  - [x] Add `GenerateQROverlayAsync(CancellationToken ct)` placeholder with try-catch logging
  - [x] Trigger placeholders in constructor using `_cts.Token`
- [x] Task 5: Registration
  - [x] Add `ReviewPrintViewModel` to DI and `MainWindowViewModel` (if needed)

## Senior Developer Review (AI)

- [x] Fix Missing Tests: Added `ReviewPrintViewModelTests.cs`.
- [x] Fix Logic Flaw / Unused Injection: Refactored `StartPrintingAsync` to be a command and implemented call to `IPrintService.PrintImageAsync`.

## Dev Notes

### Reuse Strategy: write_new (Consolidation)

We are merging the flow of `ConfirmPrintViewModel` -> `PrintingViewModel` -> `ThankYouViewModel` from `PhotoBooth.UI` into a single screen for `PhotoBooth.Event`.
- **KEEP**: 
  - The image loading pattern from `ConfirmPrintViewModel` (crucial: handle `Dispose()` on Avalonia Bitmaps).
  - The `StatusText`, `Progress`, and `_cts` management from `PrintingViewModel`.
  - The `IsOffline`, `SequentialNumber`, and restart session logic from `ThankYouViewModel`.
- **DROP**: 
  - Background loading of old generic backgrounds (nen10, nen11, etc.). The UI will be handled in `ReviewPrintView.axaml` separately.
  - The step-by-step navigation between confirm -> print -> thank you.
  - All QR-specific UI properties (`QrCodeImage`, `ShowQRScreen`, etc.) because the QR code will be burned into the final composite image directly in the next story (E4-S3).

### Project Structure Notes

- New file: `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs`

### References

- Reference old logic in:
  - `src/PhotoBooth.UI/ViewModels/ConfirmPrintViewModel.cs` (for Bitmap loading/disposing)
  - `src/PhotoBooth.UI/ViewModels/PrintingViewModel.cs` (for CTS management)
  - `src/PhotoBooth.UI/ViewModels/ThankYouViewModel.cs` (for Offline/SequentialNumber state)

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Debug Log References

### Completion Notes List

- ✅ Created `ReviewPrintViewModel` which consolidates functionality from `ConfirmPrintViewModel`, `PrintingViewModel`, and `ThankYouViewModel`.
- ✅ Handled resource management by disposing the previous `Bitmap` instance before assigning a new one.
- ✅ Implemented background task management using `CancellationTokenSource` and added proper error handling (fire-and-forget with try-catch).
- ✅ Registered `ReviewPrintViewModel` along with `PrintService` in `MainWindowViewModel`.

### File List

- `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs` (modified/recreated)
- `src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs` (modified)
- `tests/PhotoBooth.Tests/ViewModels/ReviewPrintViewModelTests.cs` (new)
- `tests/PhotoBooth.Tests/ViewModels/PhotoSelectViewModelTests.cs` (modified)
