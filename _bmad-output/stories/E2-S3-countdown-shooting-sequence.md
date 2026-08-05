# Story 2.3: countdown-shooting-sequence

Status: done

## Story

As a developer,
I want to implement the countdown and shooting sequence in the event CaptureViewModel,
so that the application can automatically capture 6 photos with a countdown timer and flash effect for each photo.

## Acceptance Criteria

1. Implement `StartShootingSequenceAsync` loop to handle the capture sequence for `TotalPhotos` (hardcoded to 6).
2. For each photo, display a countdown timer using `CountdownDuration` and update the `Countdown` property without blocking the UI thread.
3. Call `CapturePhotoAsync` inside the loop for each photo (Note: actual file saving and cropping is handled in E2-S4; this story only handles the capture loop and flash effect state).
4. Implement a short flash effect (`IsFlashing`) during the capture.
5. Provide a way to cancel the shooting sequence (via a `Skip` command) using a `CancellationTokenSource` (`_shootingCts`), ensuring instant cancellation of `Task.Delay`.
6. Ensure the logic is strictly simplified from `PhotoBooth.UI`: hardcode the count to 6 and delete all multi-layout checks and complex reconnect logic.

## Tasks / Subtasks

- [x] Task 1: Migrate Countdown Logic (Sequence & Timing)
  - [x] Implement `StartShootingSequenceAsync` loop to repeat 6 times.
  - [x] Add the countdown loop updating `IsCountingDown` and `Countdown` properties.
  - [x] Use `Task.Delay` with `_shootingCts.Token` to ensure the sequence doesn't freeze the UI and is cancellable.
- [x] Task 2: Capture Delegation & State
  - [x] Implement the `CapturePhotoAsync` method to trigger the capture and manage `IsFlashing` state.
  - [x] DO NOT implement image cropping here (handled in E2-S4). Focus only on the sequence progression and state updates.
- [x] Task 3: Simplified Cancellation
  - [x] Ensure `Skip` command cancels the `_shootingCts` and navigates.
  - [x] Navigate to `PhotoSelectViewModel` after all photos are captured.

## Dev Notes

- **Reuse Approach**: `copy_and_simplify` — countdown timer logic from `PhotoBooth.UI/ViewModels/CaptureViewModel.cs`.

### KEEP vs DELETE Guide

> [!IMPORTANT]
> When copying logic from the legacy `PhotoBooth.UI/ViewModels/CaptureViewModel.cs`, strictly follow this guide to prevent bugs and over-engineering.

**KEEP:**
- The `StartShootingSequenceAsync` while-loop and inner for-loop for countdowns.
- The `_shootingCts` usage for the `Skip` command cancellation.
- The `IsFlashing` toggle and short delays.

**DELETE COMPLETELY:**
- **Reconnect Logic**: Delete any exponential backoff or complex reconnect logic associated with camera disconnects or `_shootingCts`.
- **Layout Branching**: Delete any `if (layoutId == "layout2")` checks. The event system only uses a single 6-photo layout.
- **Dynamic Totals**: `TotalPhotos` must be strictly hardcoded to 6.

### Architectural Constraints

- **Thread Safety**: `Task.Delay` must be used instead of `Thread.Sleep`. Ensure the CancellationToken is passed to all asynchronous delays.
- **Scope Boundary**: Do not implement the `ImageCropService` logic here. That belongs to E2-S4. Only manage the index incrementing and UI state toggles.

### Project Structure Notes

- Target File: `src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs`

### References

- Source: `src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs#L354-L404` (StartShootingSequenceAsync reference)
- Source: `src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs#L406-L461` (CapturePhotoAsync reference - strip out crop logic)

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Debug Log References
None

### Completion Notes List
- Extracted logic from old CaptureViewModel.
- Simplified for Event requirements.
- Applied validation findings to optimize for Dev Agent.
- Verified all Acceptance Criteria are met.
- Marked task as complete (all tasks checked off and status set to `done`).
- [AI Review Fix] Wrapped CapturePhoto in Task.Run to prevent UI freezing.
- [AI Review Fix] Added max retry limits to avoid infinite looping on camera errors.
- [AI Review Fix] Passed cancellation token properly into capture tasks.
- [AI Review Fix] Added base CaptureViewModelTests test structure.

### File List
- src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs
- tests/PhotoBooth.Tests/ViewModels/CaptureViewModelTests.cs
