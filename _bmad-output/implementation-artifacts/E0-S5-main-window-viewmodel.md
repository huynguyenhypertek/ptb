# Story 0.5: Main Window ViewModel

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a `MainWindowViewModel` in `PhotoBooth.Event` that creates NavigationService and SessionService, registers 4 ViewModel factories, subscribes to navigation changes, and manages a shared CameraService lifetime,
so that the Event app has a fully wired navigation shell ready for screen-level stories (E1–E4).

## Acceptance Criteria

1. `MainWindowViewModel` creates a `NavigationService` instance and exposes it as a public property
2. `MainWindowViewModel` creates a `SessionService` instance and exposes it as a public property
3. `MainWindowViewModel` creates a shared `ICameraService` via `new CameraService()` as a private readonly field — singleton for the app lifetime (prevents native VideoCapture handle leaks)
4. Exactly 4 ViewModel factories registered via `NavigationService.RegisterViewModel<T>()`:
   - `StartViewModel(NavigationService, SessionService)`
   - `CaptureViewModel(NavigationService, SessionService, _sharedCameraService)`
   - `PhotoSelectViewModel(NavigationService, SessionService)`
   - `ReviewPrintViewModel(NavigationService, SessionService)`
5. `NavigationService.PropertyChanged` subscribed — updates `CurrentView` when `NavigationService.CurrentView` changes
6. `NavigationService.NavigateTo<StartViewModel>()` called at end of constructor — app starts on Start screen
7. `MainWindowViewModel` implements `IDisposable` — `Dispose()` calls `_sharedCameraService.Dispose()`
8. `MainWindow.axaml.cs` updated: subscribes to `Closed` event → calls `(DataContext as IDisposable)?.Dispose()` to ensure camera handle is released on app exit
9. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors
10. Registrations use **placeholder ViewModels** — the actual `StartViewModel`, `CaptureViewModel`, `PhotoSelectViewModel`, `ReviewPrintViewModel` classes do NOT exist yet (they're in E1–E4), so this story creates **empty stubs** that extend `ViewModelBase` with the correct constructor signatures

## Tasks / Subtasks

- [x] Task 1: Create 4 placeholder ViewModel stubs (AC: 10)
  - [x] Create `src/PhotoBooth.Event/ViewModels/StartViewModel.cs` — empty class extending `ViewModelBase`, constructor takes `(NavigationService nav, SessionService session)`, stores but does not use them
  - [x] Create `src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs` — empty class extending `ViewModelBase`, constructor takes `(NavigationService nav, SessionService session, ICameraService camera)`
  - [x] Create `src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs` — empty class extending `ViewModelBase`, constructor takes `(NavigationService nav, SessionService session)`
  - [x] Create `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs` — empty class extending `ViewModelBase`, constructor takes `(NavigationService nav, SessionService session)`
- [x] Task 2: Expand MainWindowViewModel.cs (AC: 1-7)
  - [x] Add `using System;` and service/interface usings
  - [x] Add `NavigationService` public property (get-only)
  - [x] Add `SessionService` public property (get-only)
  - [x] Add `private readonly ICameraService _sharedCameraService = new CameraService();`
  - [x] Implement constructor: create services, register 4 factories, subscribe PropertyChanged, NavigateTo<StartViewModel>
  - [x] Implement `IDisposable` — `Dispose()` calls `_sharedCameraService.Dispose()`
- [x] Task 3: Update MainWindow.axaml.cs for Dispose on Close (AC: 8)
  - [x] Subscribe to `Closed` event in constructor
  - [x] In handler: `(DataContext as IDisposable)?.Dispose()`
- [x] Task 4: Verify build (AC: 9)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: write_new

This is a **write_new** story — the `MainWindowViewModel` is written from scratch, taking inspiration from the UI version's structure but simplified for 4 screens only.

### Pattern Reference: UI MainWindowViewModel

Source: [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/MainWindowViewModel.cs) (70 lines)

The UI version:
- Registers **14** ViewModels (payment, sticker, QR, layout, background, frame, etc.)
- Creates `CameraService` as field-initializer `new CameraService()`
- Uses `ObservableObject` as base class
- Subscribes to `NavigationService.PropertyChanged` to relay `CurrentView`

The Event version:
- Registers **4** ViewModels only (Start, Capture, PhotoSelect, ReviewPrint)
- Same `CameraService` singleton pattern
- Same `PropertyChanged` relay pattern
- Same `IDisposable` pattern

### Critical: Placeholder ViewModel Stubs

The 4 ViewModels (`StartViewModel`, `CaptureViewModel`, `PhotoSelectViewModel`, `ReviewPrintViewModel`) do NOT exist yet — they will be built in E1-S1, E2-S1, E3-S1, E4-S1 respectively. This story creates **empty stubs** so the project compiles. Each stub:
- Extends `ViewModelBase` (not `ObservableObject`)
- **MUST be defined as `public partial class`** — `CommunityToolkit.Mvvm` source generators require this for `[ObservableProperty]` and `[RelayCommand]` in future stories.
- Has the correct constructor signature matching the factory registration
- Stores constructor parameters as `protected readonly` fields (available for later stories)
- Has no logic — just makes the build pass

### Critical: Transient Lifetime

The `NavigationService.RegisterViewModel(() => new T(...))` pattern registers factories, not instances. This means ViewModels are **transient** — a new instance is created every time we navigate to them, and the old one is disposed. Ensure developers working on E1-E4 understand they should not assume state persists in memory.

### Critical: CameraService Import Path

```csharp
using PhotoBooth.Core.Interfaces;       // ICameraService
using PhotoBooth.Infrastructure.Services; // CameraService (concrete)
```

The project reference to `PhotoBooth.Infrastructure` already exists in `PhotoBooth.Event.csproj` (added in E0-S1).

### Critical: NavigationService.RegisterViewModel Constraint

The Event's `NavigationService.RegisterViewModel<T>()` uses `where T : ViewModelBase` constraint (see [NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs#L31)). All registered ViewModels MUST extend `ViewModelBase`, not `ObservableObject`.

### Critical: MainWindow.axaml.cs Dispose Pattern

The UI version does NOT dispose the camera on window close — this is a known gap. The Event version MUST add a `Closed` event handler to ensure the native `VideoCapture` handle is released:

```csharp
public MainWindow()
{
    InitializeComponent();
    Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
}
```

This prevents camera resource leaks when the app is closed via window manager (X button, Cmd+Q).

### Critical: DO NOT Register ViewModels That Don't Exist

Only register the 4 ViewModels listed. Do NOT add any payment, sticker, layout, QR, or other ViewModels from the UI version. The Event flow is:

```
Start → Capture → PhotoSelect → ReviewPrint → (back to Start)
```

### Expected Final Code — MainWindowViewModel.cs

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Main window ViewModel that hosts the current view.
/// Creates NavigationService, SessionService, and shared CameraService.
/// Registers factories for the 4 Event screens and starts on StartViewModel.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    public NavigationService NavigationService { get; }
    public SessionService SessionService { get; }

    /// <summary>
    /// Singleton camera service shared across all sessions.
    /// Created once at startup, disposed when app exits.
    /// Prevents native VideoCapture handle leaks from per-session creation.
    /// </summary>
    private readonly ICameraService _sharedCameraService = new CameraService();

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel()
    {
        NavigationService = new NavigationService();
        SessionService = new SessionService();

        // Register the 4 Event screen ViewModels
        NavigationService.RegisterViewModel(() => new StartViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new CaptureViewModel(NavigationService, SessionService, _sharedCameraService));
        NavigationService.RegisterViewModel(() => new PhotoSelectViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new ReviewPrintViewModel(NavigationService, SessionService));

        // Subscribe to navigation changes — relay CurrentView to MainWindow's ContentControl binding
        NavigationService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NavigationService.CurrentView))
            {
                CurrentView = NavigationService.CurrentView;
            }
        };

        // Start with the welcome screen
        NavigationService.NavigateTo<StartViewModel>();
    }

    public void Dispose()
    {
        _sharedCameraService.Dispose();
    }
}
```

### Expected Stub — StartViewModel.cs

```csharp
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Start/welcome screen ViewModel.
/// Placeholder — full implementation in E1-S1.
/// </summary>
public partial class StartViewModel : ViewModelBase
{
    protected readonly NavigationService _navigationService;
    protected readonly SessionService _sessionService;

    public StartViewModel(NavigationService navigationService, SessionService sessionService)
    {
        _navigationService = navigationService;
        _sessionService = sessionService;
    }
}
```

### Expected Stub — CaptureViewModel.cs

```csharp
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Capture screen ViewModel — manages camera preview and photo capture.
/// Placeholder — full implementation in E2-S1.
/// </summary>
public partial class CaptureViewModel : ViewModelBase
{
    protected readonly NavigationService _navigationService;
    protected readonly SessionService _sessionService;
    protected readonly ICameraService _cameraService;

    public CaptureViewModel(NavigationService navigationService, SessionService sessionService, ICameraService cameraService)
    {
        _navigationService = navigationService;
        _sessionService = sessionService;
        _cameraService = cameraService;
    }
}
```

### Expected Stub — PhotoSelectViewModel.cs

```csharp
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Photo selection screen ViewModel — user picks 4 of 6 captured photos.
/// Placeholder — full implementation in E3-S1.
/// </summary>
public partial class PhotoSelectViewModel : ViewModelBase
{
    protected readonly NavigationService _navigationService;
    protected readonly SessionService _sessionService;

    public PhotoSelectViewModel(NavigationService navigationService, SessionService sessionService)
    {
        _navigationService = navigationService;
        _sessionService = sessionService;
    }
}
```

### Expected Stub — ReviewPrintViewModel.cs

```csharp
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Review and print screen ViewModel — shows final composite, prints, navigates back to Start.
/// Placeholder — full implementation in E4-S1.
/// </summary>
public partial class ReviewPrintViewModel : ViewModelBase
{
    protected readonly NavigationService _navigationService;
    protected readonly SessionService _sessionService;

    public ReviewPrintViewModel(NavigationService navigationService, SessionService sessionService)
    {
        _navigationService = navigationService;
        _sessionService = sessionService;
    }
}
```

### Expected Update — MainWindow.axaml.cs

```csharp
using System;
using Avalonia.Controls;

namespace PhotoBooth.Event.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
```

### Project Structure Notes

- `ViewModelBase` already exists at `src/PhotoBooth.Event/ViewModels/ViewModelBase.cs` — no changes needed
- `ViewLocator` already resolves ViewModel → View by naming convention — no changes needed
- `App.axaml.cs` already creates `new MainWindowViewModel()` as DataContext — no changes needed
- `MainWindow.axaml` already binds `Content="{Binding CurrentView}"` — no changes needed

### What This Story Does NOT Do

- Does NOT implement any screen logic — just stubs with correct constructor signatures
- Does NOT create Views (`.axaml` files) for the 4 screens — those are separate stories
- Does NOT modify `Program.cs` — already fully wired from E0-S4
- Does NOT modify `NavigationService` or `SessionService` — already complete from E0-S2, E0-S3

### Files to Create/Modify

```
src/PhotoBooth.Event/
├── ViewModels/
│   ├── MainWindowViewModel.cs  (MODIFY — expand from 14-line stub to ~50 lines)
│   ├── StartViewModel.cs       (NEW — placeholder stub, ~18 lines)
│   ├── CaptureViewModel.cs     (NEW — placeholder stub, ~21 lines)
│   ├── PhotoSelectViewModel.cs (NEW — placeholder stub, ~18 lines)
│   └── ReviewPrintViewModel.cs (NEW — placeholder stub, ~18 lines)
└── Views/
    └── MainWindow.axaml.cs     (MODIFY — add Closed handler, 1 line)
```

### References

- [Source: PhotoBooth.UI/ViewModels/MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/MainWindowViewModel.cs) — pattern reference (70 lines, 14 registrations)
- [Current: PhotoBooth.Event/ViewModels/MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs) — stub to expand (14 lines)
- [PhotoBooth.Event/ViewModels/ViewModelBase.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/ViewModelBase.cs) — base class for all ViewModels
- [PhotoBooth.Event/Services/NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs) — RegisterViewModel and NavigateTo APIs
- [PhotoBooth.Event/Services/SessionService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/SessionService.cs) — session state manager
- [PhotoBooth.Core/Interfaces/ICameraService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Core/Interfaces/ICameraService.cs) — shared camera interface
- [PhotoBooth.Event/Views/MainWindow.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml) — ContentControl binding
- [PhotoBooth.Event/Views/MainWindow.axaml.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml.cs) — code-behind to add Closed handler
- [PhotoBooth.Event/App.axaml.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/App.axaml.cs) — creates MainWindowViewModel as DataContext
- [E0-S4 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E0-S4-device-config-args.md) — previous story patterns and learnings
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E0-S5 entry line 47

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

None — clean implementation with no errors.

### Completion Notes List

- ✅ Created 4 placeholder ViewModel stubs (StartViewModel, CaptureViewModel, PhotoSelectViewModel, ReviewPrintViewModel) with correct constructor signatures and `public partial class` for CommunityToolkit.Mvvm source generator compatibility
- ✅ Expanded MainWindowViewModel from 14-line stub to full implementation: creates NavigationService + SessionService, field-initializes shared CameraService, registers 4 factories, subscribes PropertyChanged relay, navigates to StartViewModel, implements IDisposable
- ✅ Added Closed event handler in MainWindow.axaml.cs to dispose DataContext (releases native VideoCapture handle on app exit)
- ✅ Build verified: `dotnet build` succeeds with 0 errors (only pre-existing NU1903 NuGet advisory warnings for Tmds.DBus.Protocol)
- All ACs (1-10) satisfied

### Change Log

- 2026-08-04: Story E0-S5 implemented — MainWindowViewModel expanded, 4 ViewModel stubs created, MainWindow.axaml.cs updated with dispose handler
- 2026-08-04: Code review — 4 MEDIUM + 3 LOW findings. Fixed: idempotent Dispose with _disposed guard, named PropertyChanged handler with unsubscribe, trailing whitespace, stale ViewModelBase docstring. Accepted: eager CameraService init (matches spec), stub param docs (deferred to E1-E4).

### File List

- `src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs` (MODIFIED — expanded from 14 to ~64 lines)
- `src/PhotoBooth.Event/ViewModels/StartViewModel.cs` (NEW — placeholder stub, ~20 lines)
- `src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs` (NEW — placeholder stub, ~24 lines)
- `src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs` (NEW — placeholder stub, ~20 lines)
- `src/PhotoBooth.Event/ViewModels/ReviewPrintViewModel.cs` (NEW — placeholder stub, ~20 lines)
- `src/PhotoBooth.Event/Views/MainWindow.axaml.cs` (MODIFIED — added Closed dispose handler, 1 line added)
- `src/PhotoBooth.Event/ViewModels/ViewModelBase.cs` (MODIFIED — updated stale docstring)

### Senior Developer Review (AI)

**Reviewer:** Nguyenhuy on 2026-08-04
**Outcome:** ✅ Approved — all findings fixed

**Issues Found:** 0 High, 4 Medium, 3 Low

| ID | Severity | Description | Resolution |
|----|----------|-------------|------------|
| M1 | MEDIUM | `Dispose()` not idempotent — double-dispose risk on CameraService | Fixed: added `_disposed` guard + `GC.SuppressFinalize(this)` |
| M2 | MEDIUM | PropertyChanged handler never unsubscribed — leak potential | Fixed: replaced lambda with named `OnNavigationServicePropertyChanged`, unsubscribed in `Dispose()` |
| M3 | MEDIUM | CameraService eagerly instantiated at field init | Accepted: matches UI pattern and story spec, no change |
| M4 | MEDIUM | Trailing blank line in MainWindow.axaml.cs | Fixed: removed |
| L1 | LOW | ViewModelBase docstring stale (referenced E0-S2/S3 as future) | Fixed: updated docstring |
| L2 | LOW | No XML param docs on stub constructors | Accepted: stubs will be replaced in E1-E4 |
| L3 | LOW | Story line counts approximate | Not actionable |

**AC Validation:** All 10 ACs verified as IMPLEMENTED.
**Build:** `dotnet build` — 0 errors, 1 pre-existing NuGet warning (NU1903 Tmds.DBus.Protocol).

