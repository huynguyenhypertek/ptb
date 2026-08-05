# Story 2.2: Camera Preview Integration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want to see a live camera preview when I enter the capture screen,
so that I can pose and position myself correctly before the photos are taken.

## Acceptance Criteria

1. `CaptureView.axaml` exists at `src/PhotoBooth.Event/Views/CaptureView.axaml` as a `UserControl`
2. `CaptureView.axaml.cs` codebehind exists at `src/PhotoBooth.Event/Views/CaptureView.axaml.cs` — constructor calls `InitializeComponent()` only
3. XAML namespace: `x:Class="PhotoBooth.Event.Views.CaptureView"`, `x:DataType="vm:CaptureViewModel"`
4. View binds `CameraPreview` to an `<Image>` element wrapped in a `<Viewbox Stretch="Uniform">` so the live camera feed maintains its aspect ratio when `IsCameraReady` is true
5. View includes a Flash effect overlay bound to `IsFlashing`
6. View includes a `Skip` button bound to `SkipCommand` (for testing navigation and cancellation)
7. View includes a `Start` button bound to `StartShootingSequenceCommand`, which hides itself when `IsShootingInProgress` is true
8. View displays the current photo count by binding to `CurrentPhotoIndex` and `TotalPhotos`
9. View displays the countdown by binding to `Countdown` and is only visible when `IsCountingDown` is true
10. ViewLocator resolves `CaptureViewModel` → `CaptureView` correctly
11. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors
12. This is a **placeholder** view — functional bindings for camera preview and basic controls only, visual design will be finalized later
13. **NO** layout selection UI or layout-specific counter positioning (as removed in ViewModel)

## Tasks / Subtasks

- [x] Task 1: Create CaptureView.axaml (AC: 1, 3, 4, 5, 6, 7, 8, 9, 12, 13)
  - [x] Create `src/PhotoBooth.Event/Views/CaptureView.axaml`
  - [x] Set `x:Class="PhotoBooth.Event.Views.CaptureView"`, `x:DataType="vm:CaptureViewModel"`
  - [x] Add `xmlns:vm="using:PhotoBooth.Event.ViewModels"`
  - [x] Add `<Viewbox Stretch="Uniform">` containing the `<Image>` bound to `CameraPreview` and `IsCameraReady`
  - [x] Add UI text blocks binding to `CurrentPhotoIndex` / `TotalPhotos`
  - [x] Add UI text block for `Countdown`, visible when `IsCountingDown` is true
  - [x] Add Flash effect overlay (e.g., White Border) bound to `IsFlashing`
  - [x] Add functional placeholder buttons for `StartShootingSequenceCommand` (with `IsVisible="{Binding !IsShootingInProgress}"`) and `SkipCommand`
  - [x] Add text block for `StatusMessage`
- [x] Task 2: Create CaptureView.axaml.cs codebehind (AC: 2)
  - [x] Create `src/PhotoBooth.Event/Views/CaptureView.axaml.cs`
  - [x] Constructor with `InitializeComponent()` only
- [x] Task 3: Verify build and ViewLocator resolution (AC: 10, 11)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj`

## Dev Notes

### Reuse Strategy: write_new

The `PhotoBooth.UI/Views/CaptureView.axaml` is a good reference for how the `CameraPreview` is bound (using a `Viewbox` with `Stretch="Uniform"`), but it contains layout selection logic (Layout2 vs Layout6) and uses missing image assets (like `next.png` and background images). For the Event project, create a clean placeholder UI using Avalonia primitives.

**Key Bindings:**
- `CameraPreview` (Bitmap) -> `<Image Source="{Binding CameraPreview}" Stretch="UniformToFill" IsVisible="{Binding IsCameraReady}"/>` (Wrap the container in `<Viewbox Stretch="Uniform">`)
- `CurrentPhotoIndex` & `TotalPhotos` (int) -> Combine with a `MultiBinding` or adjacent `TextBlock`s to display progress (e.g., "1 / 6")
- `Countdown` (int) & `IsCountingDown` (bool) -> `TextBlock` showing the countdown number, bound to `IsVisible="{Binding IsCountingDown}"`
- `IsFlashing` (bool) -> White overlay `Border` with `IsVisible="{Binding IsFlashing}" Opacity="0.9"`
- `StartShootingSequenceCommand` -> Start button with `IsVisible="{Binding !IsShootingInProgress}"`
- `SkipCommand` -> Skip button
- `StatusMessage` (string) -> TextBlock

**Design Context Consideration:**
Do NOT attempt to add `<Design.DataContext>` since `CaptureViewModel` does not have a parameterless constructor. The app will launch via the navigation service during runtime to verify the view.

### Project Structure Notes

- New files: `src/PhotoBooth.Event/Views/CaptureView.axaml` and `src/PhotoBooth.Event/Views/CaptureView.axaml.cs`
- Must use `PhotoBooth.Event.Views` namespace and match `CaptureView` name for ViewLocator to resolve correctly from `CaptureViewModel`.

### References

- [ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs)
- [CaptureViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs)
- [UI CaptureView Reference](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Views/CaptureView.axaml)

## Dev Agent Record

### Agent Model Used

Gemini 3.1 Pro (High)

### Debug Log References

### Completion Notes List

- Fixed AC 4: Wrapped `CameraPreview` Image in `<Viewbox Stretch="Uniform">` and bound `IsVisible` to `IsCameraReady`.
- Fixed AC 8: Updated photo count text to use `MultiBinding` for both `CurrentPhotoIndex` and `TotalPhotos`.
- Added missing File List.

### File List

- `src/PhotoBooth.Event/Views/CaptureView.axaml`
- `src/PhotoBooth.Event/Views/CaptureView.axaml.cs`
