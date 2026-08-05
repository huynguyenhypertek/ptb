# Story 5.2: Admin Launcher Event Mode

Status: done

## Story

As an Admin,
I want to toggle the launch mode in the Device Launcher,
so that I can launch either the main PhotoBooth UI or the PhotoBooth Event app for my devices.

## Acceptance Criteria

1. The `DeviceLauncherView` has a UI element (e.g. ComboBox or Toggle) to choose the application to launch (UI or Event).
2. The `DeviceLauncherViewModel` respects this toggle when `LaunchDevice` is called.
3. In Dev Mode, `dotnet run` is executed targeting `PhotoBooth.Event` instead of `PhotoBooth.UI` when Event mode is selected.
4. In Packaged Mode, the launcher looks for the Event binary (e.g., `event/PhotoBooth.Event.app/Contents/MacOS/PhotoBooth.Event`) and launches it.

## Tasks / Subtasks

- [x] Task 1: Update `DeviceLauncherViewModel.cs` (AC: 1, 2, 3, 4)
  - [x] Add explicit state using an enum (e.g., `public enum LaunchMode { UI, Event }`) or a simple boolean `IsEventMode` to prevent overcomplicated bindings.
  - [x] Update `LaunchDevice` to branch logic based on selected mode.
  - [x] Add `FindEventBinary()` logic using the `copy_and_simplify` pattern from `FindUiBinary()`.
  - [x] Update Dev mode directory resolution to point to `PhotoBooth.Event` when in Event mode.
- [x] Task 2: Update `DeviceLauncherView.axaml` (AC: 1)
  - [x] Add an Avalonia `<ComboBox>` (with `SelectedIndex` binding) or a `<ToggleSwitch>` next to the "Tải lại" button to choose the mode.
  - [x] Bind to the ViewModel's launch mode state cleanly.
- [x] Task 3: Add Unit Tests
  - [x] Update or create `DeviceLauncherViewModelTests.cs` to verify that `IsEventMode = true` resolves to the Event binary path, and `IsEventMode = false` resolves to the UI binary path.

## Dev Notes

- **Existing Pattern**: The logic for finding the UI binary is `FindUiBinary()`. Create a `FindEventBinary()` that looks in `"event", "PhotoBooth.Event.app", "Contents", "MacOS", "PhotoBooth.Event"`.
- **Arguments / Data Flow**: `PhotoBooth.Event` expects the exact same arguments (`--deviceId`, `--googleDriveEnabled`, etc.) and parses them natively in its `DeviceConfig.cs`.
  - **CRITICAL**: Do NOT duplicate the argument building logic. Rename `BuildUiArgs` to `BuildLaunchArgs` and completely reuse it for both UI and Event modes.

### Project Structure Notes

- Modify `src/PhotoBooth.Admin/ViewModels/DeviceLauncherViewModel.cs`
- Modify `src/PhotoBooth.Admin/Views/DeviceLauncherView.axaml`
- Modify or create `tests/PhotoBooth.Admin.Tests/ViewModels/DeviceLauncherViewModelTests.cs`

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Completion Notes List
- Ultimate context engine analysis completed - comprehensive developer guide created

### File List
- `PhotoBooth.slnx`
- `src/PhotoBooth.Admin/ViewModels/DeviceLauncherViewModel.cs`
- `src/PhotoBooth.Admin/Views/DeviceLauncherView.axaml`
- `tests/PhotoBooth.Admin.Tests/PhotoBooth.Admin.Tests.csproj`
- `tests/PhotoBooth.Admin.Tests/ViewModels/DeviceLauncherViewModelTests.cs`

### Senior Developer Review (AI)
- **Status:** Approved with changes
- **Changes Applied:** Refactored `LaunchDevice` into `CreateLaunchStartInfo` to enable unit testing of the binary path resolution logic. Added comprehensive tests for this logic in `DeviceLauncherViewModelTests.cs`. Added missing files to the Dev Agent Record.
