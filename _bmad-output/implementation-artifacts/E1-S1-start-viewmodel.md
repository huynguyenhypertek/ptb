# Story 1.1: Start ViewModel

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want to see a welcome screen with a start button,
so that I can begin a new photo session with a single tap.

## Acceptance Criteria

1. `StartViewModel` replaces the current placeholder stub with a simplified port from `PhotoBooth.UI/ViewModels/StartViewModel.cs`
2. `Start` command calls `SessionService.StartNewSession()` then navigates to `CaptureViewModel`
3. **NO** device lock check (`CheckDeviceStatusAsync`, `IsDeviceLocked`, `LockMessage` — all removed)
4. **NO** payment-related logic (none exists in source, but confirm no stale references)
5. **NO** `IDisposable` implementation (no CancellationTokenSource, no async ops)
6. **NO** `HttpService` or `DeviceConfig` dependencies (no API calls)
7. **NO** `DeviceStatusResponse` class (remove entirely)
8. Constructor signature unchanged: `(NavigationService, SessionService)` — matches existing factory registration in `MainWindowViewModel`
9. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors
10. Class remains `public partial class StartViewModel : ViewModelBase` (partial required for CommunityToolkit.Mvvm source generators)
11. **NO** `PrepareSessionDirectory()` call in StartViewModel — CaptureViewModel (E2-S1) handles session directory creation with fallback pattern

## Tasks / Subtasks

- [x] Task 1: Replace StartViewModel.cs stub with simplified port (AC: 1, 2, 3, 4, 5, 6, 7, 8, 10, 11)
  - [x] Replace `src/PhotoBooth.Event/ViewModels/StartViewModel.cs` contents
  - [x] Add `[RelayCommand]` on `Start()` method
  - [x] `Start()` body: `_sessionService.StartNewSession()` → `_navigationService.NavigateTo<CaptureViewModel>()`
  - [x] Keep `using CommunityToolkit.Mvvm.Input;` for `[RelayCommand]`
  - [x] Verify NO `IsDeviceLocked` guard in `Start()` — always allow start
  - [x] Verify NO `IDisposable`, NO `CancellationTokenSource`, NO async
  - [x] Verify NO `PrepareSessionDirectory()` call — deferred to CaptureViewModel (E2-S1)
- [x] Task 2: Verify build (AC: 9)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: copy_and_simplify

Source: [PhotoBooth.UI/ViewModels/StartViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/StartViewModel.cs) (98 lines)
Target: [PhotoBooth.Event/ViewModels/StartViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/StartViewModel.cs) (20 lines stub → ~25 lines final)

**KEEP from source:**
- `[RelayCommand]` attribute on `Start()` method
- `_sessionService.StartNewSession()` call inside `Start()`

**REMOVE from source:**
- `IsDeviceLocked` / `LockMessage` observable properties (device lock check)
- `CheckDeviceStatusAsync()` method (API call to check device status)
- `RetryCheck()` relay command (retry device check)
- `IDisposable` implementation + `_disposed` field + `CancellationTokenSource`
- `using System;`, `using System.Net.Http.Json;`, `using System.Threading;` (no longer needed)
- `DeviceStatusResponse` class at bottom of file
- `IsDeviceLocked` guard in `Start()` — Event flow has no lock concept
- `: base(navigationService, sessionService)` constructor call — Event `ViewModelBase` has no params

**CHANGE from source:**
- Navigate to `CaptureViewModel` instead of `LayoutSelectionViewModel`
- Store `NavigationService` and `SessionService` as `protected readonly` fields (same as current stub pattern, NOT inherited from base)

### Critical: Event ViewModelBase vs UI ViewModelBase

| | Event | UI |
|---|---|---|
| Base | `ObservableObject` (no constructor params) | `ObservableObject` (constructor takes nav + session) |
| Fields | Each VM stores nav/session as own fields | Base class stores them as `protected readonly` |
| Pattern | `_navigationService` field set in constructor | `NavigationService` inherited from base |

The Event stub already stores `_navigationService` and `_sessionService` as `protected readonly` fields. Keep this pattern — do NOT call `base(navigationService, sessionService)`.

### Critical: Navigation Target

Event flow: **Start → Capture → PhotoSelect → ReviewPrint → Start**

The `Start()` command navigates to `CaptureViewModel` (NOT `LayoutSelectionViewModel` as in UI). `CaptureViewModel` is registered in [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L39) factory.

### Critical: PrepareSessionDirectory NOT Here

In the UI flow, `PrepareSessionDirectory()` is called in `BackgroundSelectionViewModel` (between Start and Capture), with a fallback in `CaptureViewModel`. Since Event skips the middle screens, `CaptureViewModel` (E2-S1) will handle session directory creation with the same fallback pattern. Do NOT call `PrepareSessionDirectory()` in StartViewModel — that's CaptureViewModel's responsibility.

### Critical: RelayCommand Source Generator

`[RelayCommand]` on `private void Start()` auto-generates `public IRelayCommand StartCommand { get; }` via CommunityToolkit.Mvvm source generators (`CommunityToolkit.Mvvm 8.4.0` in [PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj#L24)). The class MUST be `partial` for this to work. The stub is already `public partial class` — do not change this.

**XAML binding name:** The View (E1-S2) will bind to `StartCommand` (auto-generated from method name `Start`).

### Critical: IDisposable Awareness

This story does NOT implement `IDisposable` because there are no disposable resources (no CTS, no async ops, no bitmaps). However, if future modifications add disposable resources, `IDisposable` MUST be added — `NavigationService.NavigateTo<T>()` automatically calls `Dispose()` on the old ViewModel when navigating away (see [NavigationService.cs#L79-L95](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs#L79-L95)).

### Epic 0 Retrospective Action Items (Apply Here)

From [epic-0-retro](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md):
1. **`copy_and_simplify` ≠ `copy_and_trust`** — verify logic correctness of all copied code
2. **All comments in English** — translate any Vietnamese comments during copy
3. **Idempotent Dispose if applicable** — N/A for this story (no IDisposable)
4. **Named event handlers** — N/A for this story (no event subscriptions)

### Expected Final Code

```csharp
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Screen 1: Start/Welcome Screen — tap to begin a new photo session.
/// Simplified from PhotoBooth.UI version — no device lock check, no payment.
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

    [RelayCommand]
    private void Start()
    {
        _sessionService.StartNewSession();
        _navigationService.NavigateTo<CaptureViewModel>();
    }
}
```

### What This Story Does NOT Do

- Does NOT create any Views (`.axaml`) — that's E1-S2
- Does NOT add any new dependencies to `.csproj` — CommunityToolkit.Mvvm 8.4.0 already present
- Does NOT modify `MainWindowViewModel` — factory registration already correct from E0-S5
- Does NOT implement any countdown, camera, or capture logic — that's E2
- Does NOT call `PrepareSessionDirectory()` — that's CaptureViewModel's responsibility (E2-S1)

### Project Structure Notes

- File location: `src/PhotoBooth.Event/ViewModels/StartViewModel.cs` — same location as existing stub
- Naming convention: matches existing `ViewModels/` directory pattern
- No new files created, no files deleted — single file modification

### References

- [Source: PhotoBooth.UI/ViewModels/StartViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/StartViewModel.cs) — original 98 lines with device lock + payment
- [Current stub: PhotoBooth.Event/ViewModels/StartViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/StartViewModel.cs) — 20-line placeholder from E0-S5
- [ViewModelBase.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/ViewModelBase.cs) — parameterless base class
- [NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs) — `NavigateTo<T>()` API, `DisposeOldView` pattern
- [SessionService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/SessionService.cs) — `StartNewSession()`, `PrepareSessionDirectory()` APIs
- [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L38) — factory registration `() => new StartViewModel(NavigationService, SessionService)`
- [PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj#L24) — CommunityToolkit.Mvvm 8.4.0 dependency
- [E0-S5 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E0-S5-main-window-viewmodel.md) — previous story, established ViewModel patterns
- [Epic 0 Retrospective](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md) — action items for copy_and_simplify
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E1-S1 entry line 55

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

None — clean implementation, no issues encountered.

### Completion Notes List

- ✅ Replaced 20-line stub with 28-line simplified port from PhotoBooth.UI/ViewModels/StartViewModel.cs
- ✅ Removed: `CheckDeviceStatusAsync`, `IsDeviceLocked`, `LockMessage`, `RetryCheck`, `IDisposable`, `CancellationTokenSource`, `DeviceStatusResponse` class
- ✅ Removed: `HttpService`, `DeviceConfig`, all async operations
- ✅ Kept: `[RelayCommand]` on `Start()`, `_sessionService.StartNewSession()`, constructor `(NavigationService, SessionService)`
- ✅ Changed: navigation target from `LayoutSelectionViewModel` → `CaptureViewModel`
- ✅ Preserved: `protected readonly` field pattern (Event ViewModelBase has no constructor params)
- ✅ No `PrepareSessionDirectory()` call — deferred to CaptureViewModel (E2-S1)
- ✅ Build: `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — 0 errors
- ✅ All 11 acceptance criteria verified and satisfied

### Change Log

- 2026-08-04: Implemented StartViewModel — copy_and_simplify from PhotoBooth.UI, removed device lock + payment + IDisposable
- 2026-08-04: Code review fixes — added ArgumentNullException guards on constructor, added XML summary on Start() method

### File List

- `src/PhotoBooth.Event/ViewModels/StartViewModel.cs` — modified (stub → full implementation → review fixes)

## Senior Developer Review (AI)

**Reviewer:** Nguyenhuy — 2026-08-04
**Model:** Claude Opus 4.6 (Thinking)
**Outcome:** ✅ APPROVED (all fixes applied)

### Findings Summary

| Severity | Count | Status |
|---|---|---|
| 🔴 CRITICAL | 0 | — |
| 🟡 MEDIUM | 2 | 1 fixed (null guards), 1 deferred (protected visibility — cross-cutting) |
| 🟢 LOW | 2 | 1 fixed (XML doc), 1 informational (verbatim spec copy) |

### Fixes Applied

1. **M2 — Null guards:** Added `?? throw new ArgumentNullException(...)` on both constructor parameters for fail-fast behavior
2. **L1 — XML doc:** Added `/// <summary>` on `Start()` method for API documentation consistency

### Deferred

1. **M1 — `protected` → `private` field visibility:** Cross-cutting concern across all 4 ViewModels. Should be addressed as a batch change, not per-story.
