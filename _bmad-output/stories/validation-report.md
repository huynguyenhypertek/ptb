# Story Validation: E3-S3-frame-composite-preview

## 1. Context & Codebase Alignment: Valid
- **Target File**: `PhotoSelectViewModel.cs` contains the `Confirm` method, which is currently synchronous (`void`) and only sets the selected photos before navigating. It is structurally ready to be upgraded to an asynchronous command.
- **Dependencies**: `ImageCompositeService.cs` from `PhotoBooth.Infrastructure` is present and correctly matches the signature specified in the story (`string framePath, string[] photoPaths, (int x, int y, int w, int h)[] photoPositions, string outputPath`).
- **Services**: `SessionService` and `NavigationService` are already injected and available in `PhotoSelectViewModel.cs`.

## 2. Acceptance Criteria (AC) Evaluation: Valid
- ACs are well-defined and clearly address UI responsiveness by mandating `Task.Run()` for both the IO operations (Avalonia Asset loader) and the CPU-bound operation (OpenCV composite).
- ACs correctly handle duplicate execution by specifying a flag (`_isCompositing`) or using `[RelayCommand]`'s built-in `IsRunning`.
- Cleanup logic is properly placed in a `finally` block to delete temporary asset files.

## 3. Implementation Blueprint Feasibility: Valid
The `Dev Notes` provide a highly precise code snippet that maps perfectly to the existing codebase state:
- It uses the correct Avalonia asset path (`avares://PhotoBooth.Event/Assets/finish/nen6_1.png`).
- It extracts the `CapturedPhotoPaths` from `SessionService.CurrentSession` accurately.
- It correctly structures the `(int, int, int, int)` tuples required by `ImageCompositeService.Compose`.

## Conclusion
The story **E3-S3-frame-composite-preview** is **valid and ready for development**. The technical approach is sound, and all referenced services and files exist and match the expected contracts.

To proceed with implementing this story, you can use the `/dev-story` workflow.
