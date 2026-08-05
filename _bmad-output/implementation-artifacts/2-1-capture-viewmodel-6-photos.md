# Story 2.1: Capture ViewModel — 6 Photos

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want the camera to automatically capture 6 photos with a countdown between each shot,
so that I have enough photos to select the best 4 for my final printed strip.

## Acceptance Criteria

1. `CaptureViewModel` replaces the current 23-line placeholder stub with a simplified port from `PhotoBooth.UI/ViewModels/CaptureViewModel.cs`
2. `TotalPhotos` is hardcoded to **6** — no `SelectedLayout?.CaptureCount` lookup (Event has no layout selection)
3. Camera initialization: `_cameraService.Initialize(0)` with fallback to index `1`, executed on UI thread (macOS camera permission prompt requirement)
4. Subscribe ONLY to `FrameReady` event — do NOT subscribe to `CameraError` (no handler, no reconnect logic)
5. `OnFrameReady` handler renders camera preview frames to `CameraPreview` Bitmap property with frame-drop backpressure (skip frame if UI still rendering previous)
6. `StartShootingSequenceAsync` [RelayCommand]: countdown → capture → delay loop for all 6 photos, with `_shootingCts` for cancellation by `Skip` command. **Post-loop navigation**: after the while-loop exits (whether all photos succeeded or some failed), stop camera on background thread and navigate to `PhotoSelectViewModel` — ensures the user is never stuck on the capture screen
7. `CapturePhotoAsync`: calls `_cameraService.CapturePhoto(_photosDirectory)`, crops photo via `ImageCropService.CropToSize(path, 659, 720, offsetX: 50)` (hardcoded layout6 dimensions), stores path in `SessionService`. On failure: logs error, skips failed photo, continues to next (does NOT abort entire sequence). Does NOT navigate — navigation is handled by `StartShootingSequenceAsync` post-loop
8. After all 6 photos captured (or loop exits due to failures): stops camera preview on background thread, navigates to `PhotoSelectViewModel`. This navigation lives in `StartShootingSequenceAsync` AFTER the while-loop, NOT inside `CapturePhotoAsync`
9. `Skip` [RelayCommand]: checks `_disposed` guard first, cancels any in-progress shooting via `_shootingCts`, stops camera on background thread, navigates to `PhotoSelectViewModel`
10. Implements `IDisposable` with idempotent dispose pattern (`_disposed` guard + `GC.SuppressFinalize`). `StopPreview()` wrapped in `try/catch (ObjectDisposedException)` for app shutdown race safety.
11. **NO** layout selection logic (`IsLayout6`, `IsLayout2`, `LoadCaptureBackground` — all removed)
12. **NO** reconnect logic (`_reconnectAttempts`, `MaxReconnectAttempts`, `_isReconnecting`, `_reconnectLock`, `_reconnectCts`, `ScheduleReconnectAsync`, `OnCameraError` — all removed)
13. **NO** `CameraError` event subscription — not subscribing means Dispose does NOT unsubscribe it either
14. **NO** `BackgroundImage` property (no layout-specific backgrounds in Event)
15. **NO** `SelectedLayout` access on `SessionService.CurrentSession` — Event sessions have no layout
16. Session directory creation: `SessionService.PrepareSessionDirectory()` with fallback pattern (same as UI source)
17. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors
18. Class remains `public partial class CaptureViewModel : ViewModelBase, IDisposable`
19. All comments in English — translate all Vietnamese comments from source
20. Constructor signature unchanged: `(NavigationService, SessionService, ICameraService)` — matches existing factory in `MainWindowViewModel`
21. `_eventSubscribed` bool guard prevents double-subscription to `FrameReady` if `InitializeCameraAsync` completes twice (defensive — shared singleton camera service is reused across sessions)
22. `Skip` command checks `_disposed` guard before executing — prevents `StopPreview()` and `NavigateTo` calls after Dispose during rapid navigation
23. Post-loop navigation fallback: `StartShootingSequenceAsync` navigates to `PhotoSelectViewModel` after the while-loop exits, regardless of how many photos succeeded — user is never stuck on capture screen even if all 6 captures fail

## Tasks / Subtasks

- [x] Task 1: Replace CaptureViewModel.cs stub with simplified port (AC: 1, 2, 3, 11, 12, 13, 14, 16, 18, 19, 20)
  - [x] Replace `src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs` contents
  - [x] Add `using` statements: `System`, `System.Collections.ObjectModel`, `System.IO`, `System.Threading`, `System.Threading.Tasks`, `Avalonia.Media.Imaging`, `Avalonia.Threading`, `CommunityToolkit.Mvvm.ComponentModel`, `CommunityToolkit.Mvvm.Input`, `PhotoBooth.Core.Interfaces`, `PhotoBooth.Infrastructure.Services`, `PhotoBooth.Event.Services`
  - [x] Hardcode `TotalPhotos = 6`
  - [x] Implement `PrepareSessionDirectory()` fallback pattern in constructor
  - [x] Implement `InitializeCameraAsync()` — simplified (no reconnect)
  - [x] Remove all layout selection logic
  - [x] Remove all reconnect logic (but keep `_shootingCts` for Skip command)
  - [x] Translate all Vietnamese comments to English
- [x] Task 2: Implement camera preview with frame-drop (AC: 4, 5, 21)
  - [x] Subscribe ONLY `FrameReady` — do NOT subscribe `CameraError`
  - [x] Add `_eventSubscribed` bool guard to prevent double-subscription on re-init
  - [x] Add `OnFrameReady` handler with `_isRenderingFrame` backpressure guard
  - [x] Bitmap creation from `byte[]` on background, `Dispatcher.UIThread.Post` for assignment
  - [x] Dispose old bitmap before assigning new
- [x] Task 3: Implement shooting sequence + Skip command (AC: 6, 7, 8, 9, 22, 23)
  - [x] `[RelayCommand] StartShootingSequenceAsync` with `_shootingCts` for cancellation
  - [x] `CapturePhotoAsync` with flash effect, `CapturePhoto`, hardcoded crop `659x720` offset `50`
  - [x] `CapturePhotoAsync` catch: log error, skip failed photo, continue loop (do NOT re-throw, do NOT navigate)
  - [x] Post-loop navigation in `StartShootingSequenceAsync`: after while-loop exits, `Task.Run(() => _cameraService.StopPreview())` then `NavigateTo<PhotoSelectViewModel>()` — covers both success and all-failures-skip scenarios
  - [x] `[RelayCommand] Skip`: check `_disposed` guard, cancel `_shootingCts`, stop camera on background thread, navigate to PhotoSelectViewModel
- [x] Task 4: Implement IDisposable (AC: 10)
  - [x] `_disposed` guard + `GC.SuppressFinalize(this)`
  - [x] Cancel `_shootingCts` if running
  - [x] `StopPreview()` wrapped in `try/catch (ObjectDisposedException)` — app shutdown race
  - [x] Unsubscribe ONLY `FrameReady` (NOT `CameraError` — never subscribed) → set `_disposed = true`
  - [x] Dispose bitmaps on UI thread
  - [x] Do NOT dispose `_cameraService` (shared singleton owned by MainWindowViewModel)
- [x] Task 5: Verify build (AC: 17)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: copy_and_simplify

Source: [PhotoBooth.UI/ViewModels/CaptureViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs) (510 lines)
Target: [PhotoBooth.Event/ViewModels/CaptureViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs) (23-line stub → ~200 lines final)

**KEEP from source:**
- Camera init with fallback: `Initialize(0)` then `Initialize(1)` on UI thread via `Dispatcher.UIThread.InvokeAsync`
- `OnFrameReady` handler with `_isRenderingFrame` backpressure and bitmap disposal
- `StartShootingSequenceAsync` relay command with countdown → capture → delay loop
- `_shootingCts` cancellation token — retained for `Skip` command cancellation (NOT for reconnect)
- `CapturePhotoAsync` with flash effect (`IsFlashing`), `CapturePhoto`, `SessionService.AddCapturedPhoto`
- Frame drop fields: `_isRenderingFrame`, `_droppedFrameCount`
- Observable properties: `CurrentPhotoIndex`, `TotalPhotos`, `Countdown`, `IsCountingDown`, `IsCameraReady`, `IsShootingInProgress`, `IsFlashing`, `CameraPreview`, `CapturedPhotos` (View-binding convenience, mirrors `SessionService.CapturedPhotoPaths`), `StatusMessage`, `CountdownDuration` (defaults to `1` = 1-second countdown per photo)
- Dispose pattern: stop preview → unsubscribe `FrameReady` → `_disposed = true` → dispose bitmaps on UI thread
- `Skip` relay command (cancel `_shootingCts`, stop camera, navigate to PhotoSelectViewModel)
- `_eventSubscribed` guard — prevents double-subscription if `InitializeCameraAsync` completes twice (defensive)

**REMOVE from source:**
- `IsLayout6`, `IsLayout2` observable properties (no multi-layout)
- `BackgroundImage` observable property + `LoadCaptureBackground()` method
- `_reconnectAttempts`, `MaxReconnectAttempts`, `_isReconnecting`, `_reconnectLock`, `_reconnectCts` fields
- `OnCameraError` handler — removed entirely
- `CameraError` event subscription — do NOT subscribe (no handler exists). `CameraService` raises event via `?.Invoke()` which is null-safe when no subscribers.
- `CameraError` event unsubscription in Dispose — never subscribed, so never unsubscribe
- `ScheduleReconnectAsync()` method
- `_shootingCts?.Cancel()` in `OnCameraError` (handler removed — but keep `_shootingCts` for Skip)
- Layout-conditional crop logic: `if (layoutId == "layout2") ... else if (layoutId == "layout6")` — replace with single hardcoded crop
- `SessionService.CurrentSession.SelectedLayout?.CaptureCount` — replace with hardcoded `6`
- `SessionService.CurrentSession.SelectedLayout?.Id` — not used
- `NavigationService.NavigateTo<PhotoSelectionViewModel>()` — Event uses `PhotoSelectViewModel`

**CHANGE from source:**
- `TotalPhotos` = hardcoded `6` (not from `SelectedLayout?.CaptureCount`)
- Crop dimensions: hardcoded `ImageCropService.CropToSize(photoPath, 659, 720, offsetX: 50)` — layout6 dimensions only
- Navigation target: `NavigateTo<PhotoSelectViewModel>()` (NOT `PhotoSelectionViewModel` — Event class name is `PhotoSelectViewModel`)
- Base class: Event `ViewModelBase` has no constructor params — store fields directly (same as stub)
- `CapturePhotoAsync` error handling: catch block does NOT re-throw — logs error, increments `CurrentPhotoIndex`, continues the `while` loop to attempt next photo
- All Vietnamese comments → English
- Status messages: all in English
- Constructor: `NavigationService` and `SessionService` stored as `private readonly` fields (not `protected` per E1-S1 review deferred item)

### Critical: Event ViewModelBase Has No Constructor Params

The Event [ViewModelBase](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/ViewModelBase.cs) extends `ObservableObject` with no constructor parameters. Do NOT call `base(navigationService, sessionService)` — that's the UI pattern. Store `_navigationService` and `_sessionService` as own fields.

The UI `CaptureViewModel` calls `: base(navigationService, sessionService)` and then accesses `NavigationService` and `SessionService` as inherited properties. The Event version must use `_navigationService` and `_sessionService` local fields.

### Critical: Navigation Target Name

The Event project uses `PhotoSelectViewModel` (NOT `PhotoSelectionViewModel` as in UI). Verify:
- [PhotoSelectViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs) — the stub exists
- [MainWindowViewModel.cs line 40](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L40) — factory registration uses `PhotoSelectViewModel`

### Critical: Hardcoded Crop Dimensions

The UI source has conditional crop logic:
```csharp
// Layout 2: CropToSize(path, 518, 720, offsetX: 50)
// Layout 6: CropToSize(path, 659, 720, offsetX: 50)
```

Event hardcodes **layout6** dimensions: `CropToSize(path, 659, 720, offsetX: 50)`.

`ImageCropService` is a static class in [PhotoBooth.Infrastructure/Services/ImageCropService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Infrastructure/Services/ImageCropService.cs) — available via project reference, no new dependency needed.

### Critical: Session Directory Preparation

The UI source has a fallback pattern in the constructor:
```csharp
if (string.IsNullOrEmpty(SessionService.CurrentSession.SessionDirectory))
{
    SessionService.PrepareSessionDirectory();
}
_photosDirectory = SessionService.CurrentSession.SessionDirectory!;
Directory.CreateDirectory(_photosDirectory);
```

In the Event flow, `StartViewModel` does NOT call `PrepareSessionDirectory()` (per E1-S1 AC#11). The CaptureViewModel constructor MUST call it. Since no prior screen calls it, the `if` guard is technically always true, but keep the guard for safety.

### Critical: Camera Service is Shared Singleton

[MainWindowViewModel.cs line 25](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L25):
```csharp
private readonly ICameraService _sharedCameraService = new CameraService();
```

The camera service is created once and shared across all sessions. CaptureViewModel must:
- **Initialize** it (safe to re-initialize — `CameraService.Initialize()` is idempotent)
- **Start/stop preview** per session
- **Subscribe/unsubscribe** FrameReady per session
- **NOT dispose** it — `MainWindowViewModel.Dispose()` owns the lifecycle

### Critical: Disposing Bitmaps on UI Thread

Avalonia `Bitmap` objects must be disposed on the UI thread. The source pattern is correct:
```csharp
Dispatcher.UIThread.InvokeAsync(() =>
{
    CameraPreview?.Dispose();
    CameraPreview = null;
});
```

### Critical: StopPreview on Background Thread

`_cameraService.StopPreview()` internally calls `_previewTask.Wait()` which blocks. If called on UI thread, it causes a freeze. Always use:
```csharp
await Task.Run(() => _cameraService.StopPreview());
```
...except in `Dispose()`, where we call it synchronously (Dispose is called from `NavigationService.DisposeOldView()` during navigation). Wrap in `try/catch (ObjectDisposedException)` for app shutdown race safety — if `MainWindowViewModel.Dispose()` already disposed the shared `CameraService`, `StopPreview` will throw.

### Critical: IDisposable Dispose Order

From the UI source (and epic-0 retro action item #2), the dispose order is critical:
1. Cancel `_shootingCts` if running — stops any in-progress shooting sequence
2. Stop preview wrapped in `try/catch (ObjectDisposedException)` — prevents `OnFrameReady` from firing
3. Unsubscribe ONLY `FrameReady` (NOT `CameraError` — never subscribed) → set `_eventSubscribed = false`
4. Set `_disposed = true` AFTER unsubscribe
5. Dispose bitmaps on UI thread — safe since camera stopped
6. Do NOT dispose `_cameraService` — shared singleton

Add `_disposed` guard + `GC.SuppressFinalize(this)` per epic-0 retro action item.

```csharp
public void Dispose()
{
    if (_disposed) return;

    // 1. Cancel shooting if running
    _shootingCts?.Cancel();
    _shootingCts?.Dispose();
    _shootingCts = null;

    // 2. Stop preview — wrapped for shutdown race
    try { _cameraService.StopPreview(); }
    catch (ObjectDisposedException) { }

    // 3. Unsubscribe ONLY FrameReady (CameraError was never subscribed)
    _cameraService.FrameReady -= OnFrameReady;
    _eventSubscribed = false;

    // 4. Set disposed
    _disposed = true;

    // 5. Dispose bitmaps on UI thread
    Dispatcher.UIThread.InvokeAsync(() =>
    {
        CameraPreview?.Dispose();
        CameraPreview = null;
    });

    GC.SuppressFinalize(this);
}
```

### Critical: No Reconnect Logic

The sprint-status.yaml explicitly says:
> BỎ: layout selection, reconnect logic phức tạp, _shootingCts, multi-layout

Remove the entire reconnect subsystem:
- No `OnCameraError` handler — if camera fails, it simply fails
- No `CameraError` event subscription — `CameraService` raises via `?.Invoke()` which is null-safe
- No exponential backoff reconnect logic
- No `_reconnectAttempts`, `_isReconnecting`, `_reconnectLock`, `_reconnectCts`

**But KEEP `_shootingCts`** — it is still needed for the `Skip` command to cancel an in-progress shooting sequence. The sprint-status's "BỎ _shootingCts" refers to the reconnect-driven cancellation pattern, not the basic skip functionality.

This simplifies the code dramatically (~150 lines removed).

### Critical: InitializeCameraAsync Error Handling

Without reconnect, `InitializeCameraAsync` is much simpler:
- Try `Initialize(0)`, fallback `Initialize(1)`, on UI thread
- If success: subscribe ONLY `FrameReady` (NOT `CameraError`), `StartPreview()`, set `IsCameraReady = true`
- If fail: set `StatusMessage = "Camera not found!"`
- Wrap in try-catch per UI source pattern (async void must never throw)

### Critical: Shooting Sequence — Retained _shootingCts for Skip

The `_shootingCts` is simpler than the UI version — only used for `Skip` cancellation, NOT reconnect:
- `StartShootingSequenceAsync` creates `_shootingCts = new CancellationTokenSource()`
- Countdown `Task.Delay(1000, _shootingCts.Token)` and inter-photo `Task.Delay(1000, _shootingCts.Token)` are cancellable
- `Skip` command calls `_shootingCts?.Cancel()` then stops camera and navigates
- Catch `OperationCanceledException` in the shooting loop — set `StatusMessage`, reset `IsCountingDown`
- `finally` block: `IsShootingInProgress = false`, dispose `_shootingCts`
- **Post-loop navigation**: After the while-loop exits normally (not via `OperationCanceledException` from Skip), stop camera and navigate. This ensures navigation even if all 6 captures failed:

```csharp
[RelayCommand]
private async Task StartShootingSequenceAsync()
{
    if (!IsCameraReady || IsShootingInProgress) return;

    IsShootingInProgress = true;
    _shootingCts = new CancellationTokenSource();
    if (CurrentPhotoIndex >= TotalPhotos) CurrentPhotoIndex = 0;
    StatusMessage = "Starting capture...";

    try
    {
        while (CurrentPhotoIndex < TotalPhotos)
        {
            _shootingCts.Token.ThrowIfCancellationRequested();
            // 1. Countdown
            IsCountingDown = true;
            for (int i = CountdownDuration; i > 0; i--)
            {
                _shootingCts.Token.ThrowIfCancellationRequested();
                Countdown = i;
                StatusMessage = $"Photo {CurrentPhotoIndex + 1}/{TotalPhotos} in {i}s...";
                await Task.Delay(1000, _shootingCts.Token);
            }
            IsCountingDown = false;
            // 2. Capture
            await CapturePhotoAsync();
            // 3. Short delay between photos
            if (CurrentPhotoIndex < TotalPhotos)
            {
                await Task.Delay(1000, _shootingCts.Token);
            }
        }

        // Post-loop: navigate after all photos (success or partial failure)
        StatusMessage = "Complete! Transitioning...";
        await Task.Delay(1000, _shootingCts.Token);
        await Task.Run(() => _cameraService.StopPreview());
        _navigationService.NavigateTo<PhotoSelectViewModel>();
    }
    catch (OperationCanceledException)
    {
        // Skip command cancelled — Skip handles its own navigation
        StatusMessage = "Capture cancelled.";
        IsCountingDown = false;
    }
    finally
    {
        IsShootingInProgress = false;
        _shootingCts?.Dispose();
        _shootingCts = null;
    }
}
```

### Critical: CapturePhotoAsync Error Recovery — Continue, Don't Abort

If `CapturePhoto` or `CropToSize` fails for one photo:
- Log the error to console
- Increment `CurrentPhotoIndex` (skip the failed photo)
- Set `StatusMessage` with error info
- **Do NOT re-throw** — let the `while` loop continue to the next photo
- The user gets fewer than 6 photos but the session continues

```csharp
private async Task CapturePhotoAsync()
{
    try
    {
        StatusMessage = "📸 Capture!";
        IsFlashing = true;
        var photoPath = _cameraService.CapturePhoto(_photosDirectory);
        await Task.Delay(200, _shootingCts?.Token ?? CancellationToken.None);
        IsFlashing = false;
        _shootingCts?.Token.ThrowIfCancellationRequested();
        ImageCropService.CropToSize(photoPath, 659, 720, offsetX: 50);
        CapturedPhotos.Add(photoPath);
        _sessionService.AddCapturedPhoto(photoPath);
        CurrentPhotoIndex++;
        StatusMessage = $"Captured {CurrentPhotoIndex}/{TotalPhotos}";
        // NOTE: Navigation is NOT here — it's in StartShootingSequenceAsync post-loop
    }
    catch (OperationCanceledException) { throw; } // Let Skip propagate
    catch (Exception ex)
    {
        Console.WriteLine($"[CAPTURE] Photo {CurrentPhotoIndex + 1} failed: {ex.Message}");
        IsFlashing = false;
        CurrentPhotoIndex++; // Skip failed photo, continue to next
        StatusMessage = $"Photo failed, continuing... ({CurrentPhotoIndex}/{TotalPhotos})";
        // Do NOT re-throw — let while loop continue
    }
}
```

### Critical: Using Avalonia.Platform

The UI source uses `using Avalonia.Platform;` for `AssetLoader.Open(new Uri(...))` to load background images. Since Event CaptureViewModel has NO background image loading, this `using` is NOT needed.

### Critical: Skip Command Must Cancel Shooting

The `Skip` relay command must:
1. Check `_disposed` guard — prevent execution after Dispose during rapid navigation
2. Cancel `_shootingCts` to abort any in-progress shooting sequence
3. Stop camera on background thread: `await Task.Run(() => _cameraService.StopPreview())`
4. Navigate to `PhotoSelectViewModel`

```csharp
[RelayCommand]
private async Task Skip()
{
    if (_disposed) return;
    _shootingCts?.Cancel();
    await Task.Run(() => _cameraService.StopPreview());
    _navigationService.NavigateTo<PhotoSelectViewModel>();
}
```

Without the `_disposed` guard, tapping Skip after Dispose (possible during rapid navigation) would call `StopPreview()` on an already-stopped camera and `NavigateTo` which creates a NEW CaptureViewModel via factory while the old one is being disposed.

Without `_shootingCts?.Cancel()`, tapping Skip during shooting leaves an orphaned async `Task.Delay` running. The `_disposed` guard in `OnFrameReady` and shooting methods prevents state corruption, but the `_shootingCts` cancellation cleanly stops the loop immediately.

### What This Story Does NOT Do

- Does NOT create `CaptureView.axaml` — that's E2-S2
- Does NOT implement camera preview integration tests — that's E2-S2
- Does NOT implement countdown UI animations — that's E2-S3
- Does NOT modify `CameraService` or `ImageCropService` — project_reference, no changes
- Does NOT add any new NuGet packages
- Does NOT modify `MainWindowViewModel` — factory registration already correct from E0-S5
- Does NOT implement reconnect logic — explicitly removed per sprint-status
- Does NOT implement multi-layout support — Event is single-layout

### Epic 0 Retrospective Action Items (Apply Here)

From [epic-0-retro](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md):
1. **`copy_and_simplify` ≠ `copy_and_trust`** — verify logic correctness of ALL copied methods. In particular:
   - `OnFrameReady`: verify bitmap disposal order (old before new)
   - `InitializeCameraAsync`: verify `_disposed` check at every async continuation
   - `CapturePhotoAsync`: verify `CurrentPhotoIndex` increments correctly
2. **All comments in English** — translate every Vietnamese comment from source
3. **Idempotent Dispose** — `_disposed` guard + `GC.SuppressFinalize(this)` (REQUIRED since class holds native Bitmap resources)
4. **Named event handlers** — `OnFrameReady` is already a named method handler, NOT a lambda. Keep this pattern.
5. **Null guards on constructor** — add `?? throw new ArgumentNullException(...)` on all 3 constructor parameters (per E1-S1 review fix)

### Previous Story Intelligence (E1-S2)

From [E1-S2-start-view-ui.md](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S2-start-view-ui.md):
- ViewLocator resolves `CaptureViewModel` → `CaptureView` (namespace `PhotoBooth.Event.Views`)
- Design dimensions: `d:DesignWidth="1920" d:DesignHeight="1080"`
- Background color: `#1a1a2e` (consistent dark theme)
- Compiled bindings: `x:DataType` required on View
- Touch-friendly sizing for kiosk/event context
- No image assets available — text-based placeholder for CaptureView (E2-S2)

### Previous Story Intelligence (E1-S1)

From [E1-S1-start-viewmodel.md](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S1-start-viewmodel.md):
- `private readonly` fields (not `protected`) — review deferred but follow this pattern
- `ArgumentNullException` guards on constructor — apply to all 3 params
- `_sessionService.StartNewSession()` is called in StartViewModel — session is fresh when CaptureViewModel initializes

### Expected Final Code Structure

```csharp
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Screen 2: Photo Capture with camera preview and countdown.
/// Captures 6 photos with hardcoded layout6 crop dimensions.
/// Simplified from PhotoBooth.UI — no layout selection, no reconnect logic.
/// </summary>
public partial class CaptureViewModel : ViewModelBase, IDisposable
{
    private readonly NavigationService _navigationService;
    private readonly SessionService _sessionService;
    private readonly ICameraService _cameraService;
    private readonly string _photosDirectory;

    [ObservableProperty] private int _currentPhotoIndex;
    [ObservableProperty] private int _totalPhotos = 6;
    [ObservableProperty] private int _countdown = 3;
    [ObservableProperty] private bool _isCountingDown;
    [ObservableProperty] private bool _isCameraReady;
    [ObservableProperty] private bool _isShootingInProgress;
    [ObservableProperty] private bool _isFlashing;
    [ObservableProperty] private Bitmap? _cameraPreview;
    [ObservableProperty] private ObservableCollection<string> _capturedPhotos = new();
    [ObservableProperty] private string _statusMessage = "Connecting camera...";
    [ObservableProperty] private int _countdownDuration = 1; // 1-second countdown per photo

    private bool _disposed;
    private volatile bool _isRenderingFrame;
    private long _droppedFrameCount;
    private bool _eventSubscribed;

    public CaptureViewModel(NavigationService navigationService, SessionService sessionService, ICameraService cameraService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));

        // Prepare session directory (StartViewModel does NOT call this — Event skips layout screens)
        if (string.IsNullOrEmpty(_sessionService.CurrentSession.SessionDirectory))
        {
            _sessionService.PrepareSessionDirectory();
        }
        _photosDirectory = _sessionService.CurrentSession.SessionDirectory!;
        Directory.CreateDirectory(_photosDirectory);

        InitializeCameraAsync();
    }

    // ... (see Tasks for full implementation details)
}
```

### Project Structure Notes

- File modified: `src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs` — stub → full implementation
- No new files created, no files deleted
- No `.csproj` changes needed — all dependencies already present:
  - `Avalonia` 11.3.11 (Bitmap, Dispatcher)
  - `CommunityToolkit.Mvvm` 8.4.0 (ObservableProperty, RelayCommand)
  - `PhotoBooth.Core` project reference (ICameraService)
  - `PhotoBooth.Infrastructure` project reference (CameraService, ImageCropService)

### References

- [Source: PhotoBooth.UI/ViewModels/CaptureViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/ViewModels/CaptureViewModel.cs) — original 510 lines with layout, reconnect, shooting CTS
- [Current stub: PhotoBooth.Event/ViewModels/CaptureViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs) — 23-line placeholder from E0-S5
- [ViewModelBase.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/ViewModelBase.cs) — parameterless base class
- [NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs) — `NavigateTo<T>()`, `DisposeOldView` auto-calls `Dispose()`
- [SessionService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/SessionService.cs) — `PrepareSessionDirectory()`, `AddCapturedPhoto()`
- [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L39) — factory `() => new CaptureViewModel(NavigationService, SessionService, _sharedCameraService)`
- [ICameraService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Core/Interfaces/ICameraService.cs) — `Initialize`, `StartPreview`, `StopPreview`, `CapturePhoto`, `FrameReady`, `CameraError`
- [ImageCropService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Infrastructure/Services/ImageCropService.cs) — `CropToSize(path, width, height, offsetX)`
- [Session.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Core/Models/Session.cs) — `SessionDirectory`, `CapturedPhotoPaths`
- [PhotoSelectViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs) — navigation target stub
- [PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj) — all dependencies present
- [E1-S1 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S1-start-viewmodel.md) — field patterns, null guards, no PrepareSessionDirectory
- [E1-S2 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S2-start-view-ui.md) — ViewLocator convention, design dimensions
- [Epic 0 Retrospective](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md) — action items for copy_and_simplify
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E2-S1 entry, reuse directives

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

Build output: `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — 0 errors, 2 pre-existing NuGet warnings (Tmds.DBus.Protocol vulnerability — unrelated)

### Completion Notes List

- Replaced 23-line placeholder stub with ~280-line simplified port from `PhotoBooth.UI/ViewModels/CaptureViewModel.cs`
- Hardcoded `TotalPhotos = 6` — no layout selection lookup
- Camera init: `Initialize(0)` with fallback to `Initialize(1)`, executed on UI thread (macOS camera permission prompt)
- Subscribe ONLY `FrameReady` — `CameraError` not subscribed (no reconnect logic)
- `OnFrameReady` with `_isRenderingFrame` backpressure guard, bitmap disposal order verified (old before new)
- `StartShootingSequenceAsync`: countdown → capture → delay loop with `_shootingCts` for Skip cancellation
- Post-loop navigation: after while-loop exits, stops camera on background thread, navigates to `PhotoSelectViewModel` — user never stuck
- `CapturePhotoAsync`: hardcoded crop `659x720` offset `50`, error recovery continues loop (does not abort)
- `OperationCanceledException` re-thrown from `CapturePhotoAsync` to let Skip propagate through `StartShootingSequenceAsync`
- `Skip` command: `_disposed` guard, cancel `_shootingCts`, stop camera on background thread, navigate
- `IDisposable`: idempotent `_disposed` guard + `GC.SuppressFinalize(this)`, proper dispose order per story Dev Notes
- All comments in English (translated all Vietnamese from source)
- `ArgumentNullException` guards on all 3 constructor parameters (per E1-S1 review pattern)
- Event `ViewModelBase` has no constructor params — uses `_navigationService` and `_sessionService` as own fields, not inherited properties
- Removed: `IsLayout6`, `IsLayout2`, `BackgroundImage`, `LoadCaptureBackground()`, all reconnect fields/methods, `OnCameraError`, `CameraError` subscription/unsubscription, `Avalonia.Platform` using, layout-conditional crop logic
- **[Code Review Fixes]**: Fixed Skip/post-loop double-navigation race by checking `_disposed`. Fixed `_shootingCts` disposal race by capturing CTS locally and passing `CancellationToken` to `CapturePhotoAsync`. Marked `_eventSubscribed` as `volatile` for thread safety. Fixed `Countdown` initial state to `0` and added `CapturedPhotos.Clear()` on shooting restart. Replaced magic numbers with named constants.

### File List

- `src/PhotoBooth.Event/ViewModels/CaptureViewModel.cs` — MODIFIED (23-line stub → full implementation)
