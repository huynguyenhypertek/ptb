# Story 0.2: Navigation Service

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a `NavigationService` in `PhotoBooth.Event` that manages view switching between screens,
so that ViewModels can navigate the simplified flow (Start → Capture → PhotoSelect → ReviewPrint) using the same pattern as `PhotoBooth.UI`.

## Acceptance Criteria

1. `src/PhotoBooth.Event/Services/NavigationService.cs` exists, compiles, and uses namespace `PhotoBooth.Event.Services`
2. `NavigationService` extends `ObservableObject` (CommunityToolkit.Mvvm) and exposes `CurrentView` as an `[ObservableProperty]`
3. `CurrentView` property type is `ViewModelBase?` (NOT `ObservableObject?`) — matches the Event project's `ViewLocator.Match()` which checks `data is ViewModelBase`
4. `RegisterViewModel<T>()` method accepts `Func<T>` factory where `T : ViewModelBase` — constrained to `ViewModelBase` (not `ObservableObject`)
5. `NavigateTo<T>()` creates a new instance via registered factory, disposes old view if `IDisposable`, and assigns to `CurrentView`
6. `NavigateTo(ViewModelBase viewModel)` overload accepts a pre-created instance, disposes old view, and assigns
7. `DisposeOldView()` safely disposes `IDisposable` views with try-catch error logging
8. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors

## Tasks / Subtasks

- [x] Task 1: Create NavigationService (AC: 1,2,3,4,5,6,7)
  - [x] Create `src/PhotoBooth.Event/Services/` directory
  - [x] Create `NavigationService.cs` — copy from `PhotoBooth.UI/Services/NavigationService.cs` and apply simplifications:
    - Change namespace: `PhotoBooth.UI.Services` → `PhotoBooth.Event.Services`
    - Change `_currentView` type: `ObservableObject?` → `ViewModelBase?`
    - Change `RegisterViewModel<T>` constraint: `where T : ObservableObject` → `where T : ViewModelBase`
    - Change factory dictionary type: `Dictionary<Type, Func<ObservableObject>>` → `Dictionary<Type, Func<ViewModelBase>>`
    - Change `NavigateTo<T>` constraint: `where T : ObservableObject` → `where T : ViewModelBase`
    - Change `NavigateTo(ObservableObject viewModel)` parameter type → `NavigateTo(ViewModelBase viewModel)`
    - Change `DisposeOldView(ObservableObject? view)` parameter type → `DisposeOldView(ViewModelBase? view)`
    - Add `using PhotoBooth.Event.ViewModels;` import
    - Keep: `using CommunityToolkit.Mvvm.ComponentModel;` (for ObservableObject base class + [ObservableProperty])
    - Keep: `using System;` and `using System.Collections.Generic;`
    - Keep: ALL dispose logic — DisposeOldView with try-catch, dispose-before-create pattern
    - Keep: Console.WriteLine logging for dispose success/failure
- [x] Task 2: Verify build (AC: 8)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: copy_and_simplify

Source: [NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Services/NavigationService.cs) (63 lines)

The original `NavigationService` is already extremely lean — no business logic to remove. The only "simplification" is tightening the type system from `ObservableObject` → `ViewModelBase` to match the Event project's `ViewLocator` contract.

### Critical: Why ViewModelBase Instead of ObservableObject

In `PhotoBooth.UI`, the NavigationService uses `ObservableObject` as the generic constraint. This works because `PhotoBooth.UI`'s ViewLocator checks `data is ViewModelBase` where `ViewModelBase : ObservableObject`.

In `PhotoBooth.Event`, the same pattern applies but we should tighten the constraint to `ViewModelBase` directly because:
1. The `ViewLocator.Match()` method returns `true` only for `ViewModelBase` instances
2. `MainWindowViewModel._currentView` is already typed as `ViewModelBase?` (set in E0-S1)
3. Tighter type constraints prevent accidentally registering non-ViewModelBase objects that would fail ViewLocator resolution

### Navigation Pattern Architecture

```
MainWindowViewModel
  ├── owns NavigationService (created in constructor)
  ├── subscribes to NavigationService.PropertyChanged
  │   └── when CurrentView changes → updates MainWindowViewModel.CurrentView
  ├── MainWindow.axaml binds ContentControl.Content to CurrentView
  └── ViewLocator resolves ViewModel → View by name convention
```

The PropertyChanged subscription bridge (`NavigationService.CurrentView` → `MainWindowViewModel.CurrentView`) is NOT part of this story — it belongs to **E0-S5 (MainWindowViewModel)** which wires up all registrations and the subscription.

### What This Story Does NOT Do

- Does NOT register any ViewModels — that's **E0-S5 (MainWindowViewModel)**
- Does NOT wire PropertyChanged subscription — that's **E0-S5**
- Does NOT call `NavigateTo<StartViewModel>()` — that's **E0-S5**
- Does NOT create SessionService — that's **E0-S3**

This story ONLY creates the `NavigationService` class file.

### Files to Create

```
src/PhotoBooth.Event/
└── Services/
    └── NavigationService.cs   (NEW — copy_and_simplify from PhotoBooth.UI)
```

### Project Structure Notes

- New `Services/` directory follows same structure as `PhotoBooth.UI/Services/`
- Namespace `PhotoBooth.Event.Services` follows project convention
- No .csproj changes needed — no new packages required (CommunityToolkit.Mvvm already present from E0-S1)

### Expected Final Code

```csharp
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.Event.ViewModels;

namespace PhotoBooth.Event.Services;

/// <summary>
/// Manages navigation between views in the application.
/// </summary>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private ViewModelBase? _currentView;

    private readonly Dictionary<Type, Func<ViewModelBase>> _viewModelFactories = new();

    public void RegisterViewModel<T>(Func<T> factory) where T : ViewModelBase
    {
        _viewModelFactories[typeof(T)] = () => factory();
    }

    public void NavigateTo<T>() where T : ViewModelBase
    {
        if (_viewModelFactories.TryGetValue(typeof(T), out var factory))
        {
            var oldView = CurrentView;

            // Dispose old view FIRST to free memory before new constructor allocates
            DisposeOldView(oldView);

            // Now create and assign — old Bitmaps already freed
            CurrentView = factory();
        }
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        var oldView = CurrentView;

        // Dispose old view FIRST to free memory
        DisposeOldView(oldView);

        // Now assign — old Bitmaps already freed
        CurrentView = viewModel;
    }

    private void DisposeOldView(ViewModelBase? view)
    {
        if (view is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
                Console.WriteLine($"Disposed: {view.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing {view.GetType().Name}: {ex.Message}");
            }
        }
    }
}
```

### References

- [Source: PhotoBooth.UI/Services/NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Services/NavigationService.cs) — original to copy from
- [ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs) — Match() checks `data is ViewModelBase`
- [ViewModelBase.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/ViewModelBase.cs) — base class for all Event ViewModels
- [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs) — current stub, will use NavigationService in E0-S5
- [E0-S1 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E0-S1-create-project-structure.md) — previous story establishing project structure
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E0-S2 entry line 41

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

- Build output: 0 errors, 1 pre-existing NuGet warning (NU1903 Tmds.DBus.Protocol — unrelated)

### Completion Notes List

- ✅ Created `src/PhotoBooth.Event/Services/NavigationService.cs` via copy_and_simplify from `PhotoBooth.UI/Services/NavigationService.cs`
- ✅ Applied all type tightening: `ObservableObject` → `ViewModelBase` across field, dictionary, generic constraints, and method parameters
- ✅ Namespace changed to `PhotoBooth.Event.Services`, added `using PhotoBooth.Event.ViewModels;`
- ✅ Kept all dispose logic (try-catch, dispose-before-create pattern, Console.WriteLine logging)
- ✅ Build verified: `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — 0 errors

### File List

- `src/PhotoBooth.Event/Services/NavigationService.cs` (NEW)
- `tests/PhotoBooth.Tests/Services/NavigationServiceTests.cs` (NEW — 12 unit tests)
- `tests/PhotoBooth.Tests/PhotoBooth.Tests.csproj` (MODIFIED — added PhotoBooth.Event reference)

## Change Log

- 2026-08-04: Created NavigationService via copy_and_simplify from PhotoBooth.UI. Tightened type constraints from ObservableObject to ViewModelBase.
- 2026-08-04: Code review fixes — M1: added warning log on unregistered VM, M2: added thread-safety contract comment, M3: created 12 unit tests, L2: added XML doc comments, L3: added comment explaining broad catch.

## Senior Developer Review (AI)

**Reviewer:** Nguyenhuy | **Date:** 2026-08-04 | **Outcome:** ✅ Approved (all issues fixed)

**Findings (0 Critical, 3 Medium, 3 Low):**
- M1 `NavigateTo<T>()` silently ignored unregistered types → **FIXED** (added warning log)
- M2 No thread-safety documentation on `_viewModelFactories` → **FIXED** (added XML remarks contract)
- M3 No unit tests → **FIXED** (12 xUnit tests covering registration, navigation, disposal, PropertyChanged)
- L1 `Console.WriteLine` logging → **Kept** (per story spec; future improvement noted)
- L2 Missing XML doc comments on public methods → **FIXED** (all 4 public methods documented)
- L3 Broad `Exception` catch in `DisposeOldView` → **FIXED** (added comment explaining rationale)
