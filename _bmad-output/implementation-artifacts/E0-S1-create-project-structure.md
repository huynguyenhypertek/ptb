# Story 0.1: Create Project Structure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a `PhotoBooth.Event` Avalonia project created with correct references to Core and Infrastructure projects,
so that subsequent stories in Epic 0 have a valid compilation target and project scaffold to build upon.

## Acceptance Criteria

1. `src/PhotoBooth.Event/PhotoBooth.Event.csproj` exists and compiles successfully (`dotnet build`)
2. `.csproj` references `PhotoBooth.Core` and `PhotoBooth.Infrastructure` — **NOT** `PhotoBooth.API` (API is a separate web process; UI talks to it via HTTP, not project reference)
3. Avalonia packages at v11.3.11 — matching `PhotoBooth.UI.csproj` exactly
4. `CommunityToolkit.Mvvm 8.4.0` included — same as `PhotoBooth.UI`
5. `QRCoder 1.6.0` included — needed by E4-S3 QR embed feature
6. `OpenCvSharp4` is **NOT** added — it arrives transitively via `PhotoBooth.Infrastructure`
7. Placeholder files exist: `Program.cs`, `App.axaml`, `App.axaml.cs`, `ViewLocator.cs`, `app.manifest`, stub Views + ViewModels
8. All namespaces use `PhotoBooth.Event` (never `PhotoBooth.UI`)
9. `PhotoBooth.Event` project added to `PhotoBooth.slnx` under the `/src/` folder
10. `dotnet build PhotoBooth.slnx` completes with zero errors

## Tasks / Subtasks

- [x] Task 1: Create project directory and .csproj (AC: 1,2,3,4,5,6)
  - [x] Create `src/PhotoBooth.Event/` directory
  - [x] Write `PhotoBooth.Event.csproj` — `OutputType=WinExe`, `TargetFramework=net10.0`, `Nullable=enable`, `AvaloniaUseCompiledBindingsByDefault=true`, `ApplicationManifest=app.manifest`
  - [x] **Pre-check**: verify actual Avalonia version in `PhotoBooth.UI.csproj` (`grep Avalonia src/PhotoBooth.UI/PhotoBooth.UI.csproj`) before writing — do not assume v11.3.11; use whatever version is live
  - [x] Add Avalonia NuGet: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Diagnostics` — all v11.3.11 (copy Diagnostics Release-exclusion condition from PhotoBooth.UI)
  - [x] Add `CommunityToolkit.Mvvm` v8.4.0
  - [x] Add `QRCoder` v1.6.0
  - [x] Add `<AvaloniaResource Include="Assets\**" />`
  - [x] Add `ProjectReference` to `..\PhotoBooth.Core\PhotoBooth.Core.csproj`
  - [x] Add `ProjectReference` to `..\PhotoBooth.Infrastructure\PhotoBooth.Infrastructure.csproj`
  - [x] Do NOT add any reference to PhotoBooth.API

- [x] Task 2: Create placeholder source files (AC: 7,8)
  - [x] Copy `app.manifest` from `PhotoBooth.UI/app.manifest` — then update the `assemblyIdentity name` attribute from `PhotoBooth.UI.Desktop` → `PhotoBooth.Event.Desktop` (one line change; all other content stays identical). **Preserve all other elements** — especially `requestedExecutionLevel` and `dpiAware`/`dpiAwareness` settings which are critical for Windows HiDPI and kiosk display
  - [x] Create empty `Assets/` directory (placeholder for future resources)
  - [x] Create `Program.cs` — mirror `PhotoBooth.UI/Program.cs` with `namespace PhotoBooth.Event`; KEEP: `OPENCV_AVFOUNDATION_SKIP_AUTH` env var (needed even though Event doesn't call OpenCV directly — Infrastructure pulls it transitively and macOS will prompt AVFoundation auth dialogs without this), global exception handlers (AppDomain + TaskScheduler), memory monitor loop, `BuildAvaloniaApp()`; REMOVE: `DeviceConfig.ParseArgs(args)` and `SessionService.CleanupStaleSessions(...)` calls (not yet implemented in this story)
  - [x] Create `App.axaml` — mirror `PhotoBooth.UI/App.axaml` with `x:Class="PhotoBooth.Event.App"`, namespace `using:PhotoBooth.Event`, FluentTheme, ViewLocator DataTemplate
  - [x] Create `App.axaml.cs` — mirror `PhotoBooth.UI/App.axaml.cs` with namespace `PhotoBooth.Event`; reference `Views.MainWindow` and `ViewModels.MainWindowViewModel`; **keep `DisableAvaloniaDataAnnotationValidation()` call** in `OnFrameworkInitializationCompleted()` — prevents duplicate validation errors from CommunityToolkit.Mvvm + Avalonia binding stack
  - [x] Create `ViewLocator.cs` — mirror `PhotoBooth.UI/ViewLocator.cs` with namespace `PhotoBooth.Event`; update `Match()` to check local `ViewModelBase`
  - [x] Create stub `Views/MainWindow.axaml` — mirror `PhotoBooth.UI/Views/MainWindow.axaml` with namespace changes; MUST include: `WindowState="Maximized"` (kiosk fullscreen), `Background="#1a1a2e"`, `x:DataType="vm:MainWindowViewModel"`, design-time DataContext, and `<ContentControl Content="{Binding CurrentView}" />` — **NOT a TextBlock placeholder** (ContentControl + ViewLocator is the required navigation pattern from day one)
  - [x] Create stub `Views/MainWindow.axaml.cs` — `public partial class MainWindow : Window`
  - [x] Create stub `ViewModels/ViewModelBase.cs` — `public abstract partial class ViewModelBase : ObservableObject` (no dependencies yet)
  - [x] Create stub `ViewModels/MainWindowViewModel.cs` — `public partial class MainWindowViewModel : ObservableObject` (no logic yet, just compiles)

- [x] Task 3: Register in solution and verify build (AC: 9,10)
  - [x] Edit `PhotoBooth.slnx` — inside `<Folder Name="/src/">`, add `<Project Path="src/PhotoBooth.Event/PhotoBooth.Event.csproj" />`
  - [x] Run `dotnet restore PhotoBooth.slnx` to pull all NuGet packages before building
  - [x] Run `dotnet build PhotoBooth.slnx` and confirm zero errors

## Dev Notes

### Reuse Strategy

This story is `write_new`. Do NOT copy-paste files from `PhotoBooth.UI` — only use them as structural references. The new project has a different namespace and simplified scope.

### Critical: Why NOT referencing PhotoBooth.API

`PhotoBooth.API` uses `Microsoft.NET.Sdk.Web`. Adding it as a `ProjectReference` into a `Microsoft.NET.Sdk` WinExe project causes SDK conflicts and pulls in ASP.NET Core web pipeline. The Event UI communicates with the API via HTTP (`HttpService.cs` — implemented in E0-S3). Never add `ProjectReference` to `PhotoBooth.API`.

### Project Reference Paths

Use **single** `..` -- `PhotoBooth.Event` is at `src/PhotoBooth.Event/`, same depth as `src/PhotoBooth.UI/`:
```xml
<ProjectReference Include="..\\PhotoBooth.Core\\PhotoBooth.Core.csproj" />
<ProjectReference Include="..\\PhotoBooth.Infrastructure\\PhotoBooth.Infrastructure.csproj" />
```

### csproj Properties -- Do NOT Add ImplicitUsings

`PhotoBooth.UI.csproj` does **not** have `<ImplicitUsings>enable</ImplicitUsings>` -- unlike Core/Infrastructure which do. Do NOT add it to `PhotoBooth.Event.csproj`. The UI projects use explicit `using` statements to avoid ambiguous type resolution.

### Solution File Format

`PhotoBooth.slnx` is the `.NET 10 Solution XML` format (not classic `.sln`). Current content:
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/PhotoBooth.Core/PhotoBooth.Core.csproj" />
    <Project Path="src/PhotoBooth.Infrastructure/PhotoBooth.Infrastructure.csproj" />
    <Project Path="src/PhotoBooth.UI/PhotoBooth.UI.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/PhotoBooth.Tests/PhotoBooth.Tests.csproj" />
  </Folder>
</Solution>
```
Add the new project inside the `/src/` folder. Place it after `PhotoBooth.UI` for logical ordering.

### File Structure to Create

```
src/PhotoBooth.Event/
├── PhotoBooth.Event.csproj
├── app.manifest                  (copy from PhotoBooth.UI/app.manifest)
├── Program.cs
├── App.axaml
├── App.axaml.cs
├── ViewLocator.cs
├── Assets/                       (empty directory)
├── Views/
│   ├── MainWindow.axaml
│   └── MainWindow.axaml.cs
└── ViewModels/
    ├── ViewModelBase.cs
    └── MainWindowViewModel.cs
```

### Avalonia Diagnostics Package Pattern (from PhotoBooth.UI.csproj)

```xml
<PackageReference Include="Avalonia.Diagnostics" Version="11.3.11">
  <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
  <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
</PackageReference>
```

### OpenCvSharp4 Note

Do NOT add `OpenCvSharp4` to `PhotoBooth.Event.csproj` — it comes transitively from `PhotoBooth.Infrastructure` (`4.11.0.20250507`). Adding it directly causes duplicate assembly conflicts.

**macOS ARM Runtime**: `PhotoBooth.UI.csproj` directly lists `OpenCvSharp4.runtime.osx_arm64 4.8.1-rc` for native macOS ARM support. This does NOT arrive transitively from Infrastructure (Infrastructure only has the managed package). If `dotnet build` fails on macOS ARM with a missing native runtime error, add:
```xml
<PackageReference Include="OpenCvSharp4.runtime.osx_arm64" Version="4.8.1-rc" />
```

### Git Intelligence

Recent commits show the project is actively used (camera service singleton sharing, bitmap disposal, print service integration). Keep the project structure consistent with existing code — same target framework `net10.0`, same Avalonia version family.

### Project Structure Notes

- The existing infrastructure project already has `OpenCvSharp4 4.11.0.20250507` — this will be available transitively
- `PhotoBooth.Core` is a plain class library with Interfaces and Models — safe reference with no side effects
- `PhotoBooth.Infrastructure` references Core and adds OpenCvSharp4 — transitively available to Event project

### References

- [PhotoBooth.UI.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/PhotoBooth.UI.csproj) — package version template
- [PhotoBooth.slnx](file:///Users/nguyenhuy/works/ptb/PhotoBooth.slnx) — solution file to update
- [PhotoBooth.Core.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Core/PhotoBooth.Core.csproj)
- [PhotoBooth.Infrastructure.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Infrastructure/PhotoBooth.Infrastructure.csproj)
- [Program.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Program.cs) — bootstrap pattern reference
- [App.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/App.axaml) + [App.axaml.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/App.axaml.cs) — Avalonia app pattern
- [ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewLocator.cs) — view resolution pattern
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E0-S1 entry line 39

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

None — clean implementation with no build failures.

### Completion Notes List

- ✅ Created `PhotoBooth.Event.csproj` with Avalonia v11.3.11 (verified from live `PhotoBooth.UI.csproj`), CommunityToolkit.Mvvm 8.4.0, QRCoder 1.6.0
- ✅ Project references Core + Infrastructure only — NO API reference (SDK conflict prevention)
- ✅ No OpenCvSharp4 direct reference (arrives transitively from Infrastructure)
- ✅ No ImplicitUsings (matching PhotoBooth.UI pattern)
- ✅ Diagnostics package has Release-exclusion condition
- ✅ All 10 placeholder files created with `PhotoBooth.Event` namespace throughout
- ✅ MainWindow uses ContentControl + CurrentView binding (navigation pattern ready)
- ✅ Program.cs keeps OPENCV_AVFOUNDATION_SKIP_AUTH, global exception handlers, memory monitor
- ✅ Program.cs removes DeviceConfig.ParseArgs and SessionService.CleanupStaleSessions (not yet in scope)
- ✅ app.manifest assemblyIdentity updated from PhotoBooth.UI.Desktop → PhotoBooth.Event.Desktop
- ✅ Project added to PhotoBooth.slnx under /src/ folder
- ✅ `dotnet build PhotoBooth.slnx` — 0 errors (6 pre-existing NU1903 warnings)
- ✅ `dotnet test` — 26/26 tests passed, 0 regressions
- ✅ [Review Fix H1/M2] `MainWindowViewModel._currentView` type changed from `ObservableObject?` → `ViewModelBase?` to match ViewLocator contract
- ✅ [Review Fix L2] `Program.cs` memory monitor: added `using` to `Process.GetCurrentProcess()` to prevent handle leaks
- ✅ [Review Fix L1] Added `Assets/.gitkeep` so empty directory is tracked by git

### Change Log

- 2026-08-04: Created PhotoBooth.Event project structure (E0-S1) — csproj, 10 source files, solution registration, build verified
- 2026-08-04: Code review fixes — ViewModelBase type safety, Process disposal, .gitkeep for Assets

### File List

- `src/PhotoBooth.Event/PhotoBooth.Event.csproj` (new)
- `src/PhotoBooth.Event/app.manifest` (new)
- `src/PhotoBooth.Event/Program.cs` (new)
- `src/PhotoBooth.Event/App.axaml` (new)
- `src/PhotoBooth.Event/App.axaml.cs` (new)
- `src/PhotoBooth.Event/ViewLocator.cs` (new)
- `src/PhotoBooth.Event/Views/MainWindow.axaml` (new)
- `src/PhotoBooth.Event/Views/MainWindow.axaml.cs` (new)
- `src/PhotoBooth.Event/ViewModels/ViewModelBase.cs` (new)
- `src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs` (new)
- `src/PhotoBooth.Event/Assets/.gitkeep` (new)
- `PhotoBooth.slnx` (modified — added PhotoBooth.Event project reference)
