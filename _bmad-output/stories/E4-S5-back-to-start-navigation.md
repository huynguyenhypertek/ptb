# Story 4.5: Back to Start Navigation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want to easily navigate back to the start screen after my photos are printed,
so that the booth is ready for the next user.

## Acceptance Criteria

1. **Back to Start Button**:
   - `ReviewPrintView` MUST include a prominent button (e.g. "Hoàn tất" or "Bắt đầu lại") bound to the existing `ReturnToStartCommand`.

2. **Session Reset & Navigation Verification**:
   - Activating `ReturnToStartCommand` MUST clear the session via `SessionService.StartNewSession()` and navigate to `StartViewModel`.
   - *Note: This logic is already present in `ReviewPrintViewModel`; it requires verification and UI binding.*

3. **Test Coverage**:
   - `ReviewPrintViewModelTests.cs` MUST include tests verifying that invoking `ReturnToStartCommand` calls both `StartNewSession()` and `NavigateTo<StartViewModel>()`.
   - MUST include tests that simulate the timer cancellation (e.g., clicking the button before 30 seconds cancels the timer to prevent double navigation) and tests that simulate the full timeout path.

4. **Auto-Reset Idle Timer**:
   - `ReviewPrintViewModel` MUST implement a 30-second idle timer that automatically executes `ReturnToStartCommand` if the user walks away.
   - The timer MUST be gracefully canceled when the user manually activates `ReturnToStartCommand` or when navigating away, ensuring no dangling tasks trigger rogue background navigation events.

## Tasks / Subtasks

- [x] Task 1: Add "Back to Start" button in `ReviewPrintView.axaml` bound to `ReturnToStartCommand` (AC: 1).
- [x] Task 2: Verify `ReturnToStart()` in `ReviewPrintViewModel.cs` correctly resets session and navigates (AC: 2).
- [x] Task 3: Write unit tests in `ReviewPrintViewModelTests.cs` for `ReturnToStartCommand`, including timer cancellation and timeout paths (AC: 3).
- [x] Task 4: Implement a 30s auto-reset idle timer in `ReviewPrintViewModel` that triggers `ReturnToStartCommand` and ensures proper cancellation (AC: 4).

## Senior Developer Review (AI)

- [x] Review completed.
- [x] Verified `ReturnToStartCommand` UI binding.
- [x] Verified Session Reset and Navigation logic.
- [x] Verified `StartIdleTimer` auto-reset and cancellation logic.
- [x] Fixed missing "In thêm" (Print More) button in `ReviewPrintView.axaml` which was required because the previous Print automation made the Copies selectors useless without a manual reprint mechanism.

## Dev Notes

- **UI Binding Syntax**: Use exact syntax `Command="{Binding ReturnToStartCommand}"` for the button in `ReviewPrintView.axaml`.
- **Existing Implementation**: `ReviewPrintViewModel.cs` already contains the `ReturnToStart()` logic. Do NOT rewrite it. Just verify it works and add the corresponding unit tests.
- **Idle Timer Details**: Use a `CancellationTokenSource` scoped to the ViewModel's lifecycle or screen activation for the 30-second timer. It MUST be cancelled when "Back to Start" is clicked or when the view model is deactivated.

### Project Structure Notes

- Files to modify/verify:
  - `src/PhotoBooth.Event/Views/ReviewPrintView.axaml`
  - `tests/PhotoBooth.Tests/ViewModels/ReviewPrintViewModelTests.cs`
  - `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs`

### References

- [Source: _bmad-output/sprint-status.yaml#development_status]

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Debug Log References

- Validated UI bindings in `ReviewPrintView.axaml`.
- Validated `ReturnToStart` correctly calls `SessionService.StartNewSession()` and triggers navigation.
- Fixed an issue in tests to override `IdleTimeoutMs` when testing the idle timeout.

### Completion Notes List

- Added `ReviewPrintView.axaml` and `ReviewPrintView.axaml.cs` with the requested Back to Start (Hoàn tất) button.
- Added `StartIdleTimer()` logic in `ReviewPrintViewModel.cs` configured for a 30-second delay. It correctly triggers `ReturnToStartCommand` if left idle, and is canceled upon navigating away or manual user actions.
- Wrote unit tests `ReturnToStartCommand_ResetsSessionAndNavigates`, `IdleTimer_NavigatesToStartAfterTimeout`, and `ReturnToStartCommand_CancelsIdleTimer`. All tests passed.

### Code Review Fixes (AI)

- **CRITICAL**: Fixed premature timeout. `StartIdleTimer()` was previously called at the beginning of `InitializeAsync()`. Now it properly resets *after* initialization finishes (so the user isn't kicked back during QR upload or printing).
- **CRITICAL**: Timer is now properly paused when the user clicks "In thêm" (`StartPrintingAsync`) and resumed afterwards, preventing unexpected navigation during printing.
- **MEDIUM**: Added untracked files `ReviewPrintView.axaml` and `.cs` to git. 
- **LOW**: Shortened the return button text to "Hoàn tất" for better UI fit.

### File List

- `src/PhotoBooth.Event/Views/ReviewPrintView.axaml`
- `src/PhotoBooth.Event/Views/ReviewPrintView.axaml.cs`
- `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs`
- `tests/PhotoBooth.Tests/ViewModels/ReviewPrintViewModelTests.cs`
