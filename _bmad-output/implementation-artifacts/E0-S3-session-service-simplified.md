# Story 0.3: Session Service (Simplified)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a simplified `SessionService` in `PhotoBooth.Event` that manages per-session state (captured photos, selections, directories),
so that ViewModels share a consistent session context across the 4-screen event flow without payment, sticker, or layout concerns.

## Acceptance Criteria

1. `src/PhotoBooth.Event/Services/SessionService.cs` exists, compiles, and uses namespace `PhotoBooth.Event.Services`
2. `SessionService` is a plain class (NOT `ObservableObject`) — same as the UI version
3. `CurrentSession` property typed `Session` (from `PhotoBooth.Core.Models`) — returns the active session
4. `StartNewSession()` resets `CurrentSession` to a fresh `Session()` — **WITHOUT** `GC.Collect()` calls or `CancellationTokenSource` (no background tasks to cancel in simplified version)
5. `PrepareSessionDirectory()` creates a timestamped session folder under `GetSessionsBaseDirectory()`, assigns `SessionDirectory` + `SessionFolderName` on `CurrentSession`, assigns `SequentialNumber` via `SequentialNumberService` — **WITHOUT** Google Drive `PreFetchDriveUrlAsync` call
6. `AddCapturedPhoto(string path)` adds to `CurrentSession.CapturedPhotoPaths`
7. `SetSelectedPhotos(List<int> indices)` assigns to `CurrentSession.SelectedPhotoIndices`
8. `SetFinalImage(string path)` assigns to `CurrentSession.FinalImagePath`
9. `SetQRCode(string url)` assigns to `CurrentSession.QRCodeUrl`
10. `GetSessionsBaseDirectory()` static method — returns Google Drive path if enabled + exists, else `~/Pictures/PhotoBooth/` fallback (same logic as UI)
11. `CleanupStaleSessions(TimeSpan maxAge)` static method — deletes session directories older than maxAge (same logic as UI)
12. The following methods from `PhotoBooth.UI` are **NOT** present: `SetLayout()`, `SetFrame()`, `SetBackground()`, `AddSticker()`, `SetPaymentComplete()`, `PreFetchDriveUrlAsync()`
13. `SequentialNumberService` — copy from `PhotoBooth.UI/Services/SequentialNumberService.cs`, change namespace to `PhotoBooth.Event.Services` (no other changes)
14. Minimal `DeviceConfig.cs` stub exists at `src/PhotoBooth.Event/DeviceConfig.cs` with `GoogleDriveEnabled`, `GoogleDrivePath`, and `GetMaskedGDrivePath()` — to be expanded in E0-S4
15. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors

## Tasks / Subtasks

- [x] Task 1: Copy and simplify SessionService (AC: 1-12, 14)
  - [x] Create `src/PhotoBooth.Event/Services/SessionService.cs`
  - [x] Copy from `PhotoBooth.UI/Services/SessionService.cs` (280 lines → ~140 lines target)
  - [x] Change namespace: `PhotoBooth.UI.Services` → `PhotoBooth.Event.Services`
  - [x] Remove `SetLayout(Layout layout)` method
  - [x] Remove `SetFrame(Frame frame)` method
  - [x] Remove `SetBackground(Background background)` method
  - [x] Remove `AddSticker(StickerPlacement sticker)` method
  - [x] Remove `SetPaymentComplete(bool isPaid)` method
  - [x] Remove `GC.Collect()` / `GC.WaitForPendingFinalizers()` block from `StartNewSession()`
  - [x] Remove `PreFetchDriveUrlAsync()` method entirely
  - [x] Remove the `PreFetchDriveUrlAsync` call inside `PrepareSessionDirectory()`
  - [x] Remove `_sessionCts` field and its cancel/dispose/recreate in `StartNewSession()` — dead code after PreFetch removal
  - [x] Remove `using System.Net.Http;`, `using System.Threading.Tasks;`, and `using System.Threading;` (no longer needed)
  - [x] Create minimal `DeviceConfig.cs` stub in `PhotoBooth.Event` (see Dev Notes → DeviceConfig Dependency)
- [x] Task 2: Copy SequentialNumberService (AC: 13)
  - [x] Create `src/PhotoBooth.Event/Services/SequentialNumberService.cs`
  - [x] Copy from `PhotoBooth.UI/Services/SequentialNumberService.cs` (137 lines)
  - [x] Change namespace: `PhotoBooth.UI.Services` → `PhotoBooth.Event.Services`
  - [x] Change `CounterState` class namespace to match
  - [x] No other changes needed — logic is identical
- [x] Task 3: Verify build (AC: 15)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: copy_and_simplify

Source: [SessionService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Services/SessionService.cs) (280 lines)

**KEEP** (core session management):
- ~~`_sessionCts` field~~ — REMOVED (dead code: only consumer was `PreFetchDriveUrlAsync`)
- `SessionsBaseDirectory` property (Google Drive path with fallback)
- `MinValidDate` constant + `GetDirectoryAgeUtc()` helper
- `CurrentSession` property
- `StartNewSession()` — cancel CTS, reset session (WITHOUT GC.Collect)
- `PrepareSessionDirectory()` — create dir, assign paths, assign sequential number (WITHOUT PreFetch)
- `AddCapturedPhoto()`, `SetSelectedPhotos()`, `SetFinalImage()`, `SetQRCode()`
- `GetSessionsBaseDirectory()` static accessor
- `CleanupStaleSessions()` static method with full error handling

**REMOVE** (event flow doesn't need these):
| Method | Reason |
|---|---|
| `SetLayout(Layout)` | Event hardcodes 1 layout (6 photos, 1 frame) |
| `SetFrame(Frame)` | Event hardcodes 1 frame |
| `SetBackground(Background)` | Event has no background selection |
| `AddSticker(StickerPlacement)` | Event has no sticker feature |
| `SetPaymentComplete(bool)` | Event has no payment |
| `GC.Collect()` block | Unnecessary for event — sessions are shorter, NavigationService DisposeOldView already handles cleanup |
| `PreFetchDriveUrlAsync()` | No QR screen in event flow — QR is embedded on print image (E4-S3) |

### Critical: DeviceConfig Dependency

The original `SessionService` references `PhotoBooth.UI.DeviceConfig` in multiple places:
- `SessionsBaseDirectory` → `DeviceConfig.GoogleDriveEnabled`, `DeviceConfig.GoogleDrivePath`
- `PrepareSessionDirectory()` → `DeviceConfig.GoogleDriveEnabled`, `DeviceConfig.AppsScriptUrl`

**For this story:** The Event project does NOT yet have its own `DeviceConfig` (that's **E0-S4**). Two options:

1. **Option A (Recommended):** Temporarily reference `PhotoBooth.UI.DeviceConfig` via using — this WILL NOT WORK because `PhotoBooth.Event.csproj` does NOT reference `PhotoBooth.UI.csproj` (and should not).

2. **Option B (Correct):** Create a minimal `DeviceConfig` stub in `PhotoBooth.Event` with just the properties `SessionService` needs (`GoogleDriveEnabled`, `GoogleDrivePath`, `GetMaskedGDrivePath()`). E0-S4 will expand it.

**→ Use Option B:** Create a minimal `DeviceConfig.cs` stub at `src/PhotoBooth.Event/DeviceConfig.cs` with:
```csharp
namespace PhotoBooth.Event;

/// <summary>
/// Minimal device configuration stub. 
/// Full implementation in E0-S4 (device-config-args).
/// </summary>
public static class DeviceConfig
{
    public static bool GoogleDriveEnabled { get; set; } = false;
    public static string GoogleDrivePath { get; set; } = "";
    
    public static string GetMaskedGDrivePath()
    {
        if (string.IsNullOrEmpty(GoogleDrivePath)) return "";
        var segments = GoogleDrivePath.Split(
            System.IO.Path.DirectorySeparatorChar, 
            System.IO.Path.AltDirectorySeparatorChar);
        if (segments.Length < 2) return ".../";
        return ".../" + string.Join("/", segments[^2..]);
    }
}
```

> [!IMPORTANT]
> Do NOT add a project reference to `PhotoBooth.UI`. The Event project must remain independent.

### Critical: SequentialNumberService Counter File Path

`SequentialNumberService` in `PhotoBooth.UI` stores its counter at `~/.photobooth/counter.json`. If both `PhotoBooth.UI` and `PhotoBooth.Event` run on the same machine, they would **share the same counter file** — this is **INTENTIONAL** for event use because sequential numbers should be unique across all apps on the device.

Keep the same path (`~/.photobooth/counter.json`).

### Critical: What to Remove from PrepareSessionDirectory()

The original `PrepareSessionDirectory()` has this block that must be removed:

```diff
     // Start background pre-fetch of Google Drive URL during payment/capture screens
-    if (DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.AppsScriptUrl))
-    {
-        _ = PreFetchDriveUrlAsync(sessionFolder, _sessionCts.Token);
-    }
```

And the entire `PreFetchDriveUrlAsync()` method (lines 115-167) is deleted. This also means:
- Remove `using System.Net.Http;` — no longer needed
- Remove `using System.Threading.Tasks;` — no longer needed
- Remove `using System.Threading;` — no longer needed (`_sessionCts` also removed)

### Critical: What to Remove from StartNewSession()

```diff
     public void StartNewSession()
     {
-        _sessionCts.Cancel();
-        _sessionCts.Dispose();
-        _sessionCts = new CancellationTokenSource();
-        
         CurrentSession = new Session();
-        
-        // Force GC between sessions to reclaim native memory from OpenCV Mat / Avalonia Bitmap
-        // objects that were disposed but not yet finalized.
-        GC.Collect();
-        GC.WaitForPendingFinalizers();
-        GC.Collect();
     }
```

> [!NOTE]
> `_sessionCts` is also removed entirely (field declaration + all usage). In the original, it only served `PreFetchDriveUrlAsync`. With PreFetch gone, `_sessionCts` has zero consumers — pure dead code.

### Navigation Pattern Architecture (from E0-S2)

```
MainWindowViewModel
  ├── owns NavigationService (created in constructor)
  ├── owns SessionService (created in constructor)
  ├── passes SessionService to ViewModel factories
  └── ViewModels use SessionService for session state
```

`SessionService` is a **dependency** that ViewModels receive via constructor injection from `MainWindowViewModel` (E0-S5).

### What This Story Does NOT Do

- Does NOT register `SessionService` in DI/MainWindowViewModel — that's **E0-S5 (MainWindowViewModel)**
- Does NOT create the full `DeviceConfig` — that's **E0-S4 (device-config-args)**
- Does NOT create `HttpService` — that's a separate story (Event doesn't need HttpService for SessionService)
- Does NOT add `AppsScriptUrl` to the Event DeviceConfig stub — not needed without PreFetch

### Files to Create

```
src/PhotoBooth.Event/
├── DeviceConfig.cs              (NEW — minimal stub, expanded in E0-S4)
└── Services/
    ├── SessionService.cs        (NEW — copy_and_simplify from PhotoBooth.UI)
    └── SequentialNumberService.cs (NEW — copy from PhotoBooth.UI, namespace change only)
```

### Expected Final Code — SessionService.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using PhotoBooth.Core.Models;

namespace PhotoBooth.Event.Services;

/// <summary>
/// Manages the current session state across all views.
/// Simplified for event flow — no payment, sticker, layout, or Google Drive pre-fetch.
/// </summary>
public class SessionService
{
    /// <summary>
    /// Base directory for all session folders.
    /// Returns Google Drive path when enabled and accessible, otherwise ~/Pictures/PhotoBooth/.
    /// </summary>
    private static string SessionsBaseDirectory
    {
        get
        {
            if (DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.GoogleDrivePath))
            {
                if (Directory.Exists(DeviceConfig.GoogleDrivePath))
                {
                    return DeviceConfig.GoogleDrivePath;
                }
                Console.WriteLine($"[WARNING] Google Drive path not found: {DeviceConfig.GetMaskedGDrivePath()} — falling back to local storage");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoBooth");
        }
    }

    /// <summary>
    /// Any directory creation date before this is considered invalid/unsupported.
    /// </summary>
    private static readonly DateTime MinValidDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Session CurrentSession { get; private set; } = new();

    public void StartNewSession()
    {
        CurrentSession = new Session();
    }

    /// <summary>
    /// Creates the session directory on disk early (before capture starts).
    /// Assigns sequential number for session identification.
    /// </summary>
    public void PrepareSessionDirectory()
    {
        var sessionFolder = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..6]}";
        var basePath = GetSessionsBaseDirectory();
        var fullPath = Path.Combine(basePath, sessionFolder);
        Directory.CreateDirectory(fullPath);

        CurrentSession.SessionDirectory = fullPath;
        CurrentSession.SessionFolderName = sessionFolder;

        Console.WriteLine($"[SESSION] Directory prepared early: {sessionFolder}");

        if (string.IsNullOrEmpty(CurrentSession.SequentialNumber))
        {
            try
            {
                CurrentSession.SequentialNumber = SequentialNumberService.GetNextNumber();
                Console.WriteLine($"[SESSION] Sequential number: {CurrentSession.SequentialNumber}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SESSION] WARNING: Failed to assign sequential number: {ex.Message}");
            }
        }
    }

    public void AddCapturedPhoto(string path)
    {
        CurrentSession.CapturedPhotoPaths.Add(path);
    }

    public void SetSelectedPhotos(List<int> indices)
    {
        CurrentSession.SelectedPhotoIndices = indices;
    }

    public void SetFinalImage(string path)
    {
        CurrentSession.FinalImagePath = path;
    }

    public void SetQRCode(string url)
    {
        CurrentSession.QRCodeUrl = url;
    }

    /// <summary>
    /// Returns the base directory where all session folders are stored.
    /// </summary>
    public static string GetSessionsBaseDirectory() => SessionsBaseDirectory;

    /// <summary>
    /// Returns the best available UTC timestamp for the directory's age.
    /// Falls back to LastWriteTimeUtc when CreationTimeUtc is unsupported (Linux).
    /// </summary>
    private static DateTime GetDirectoryAgeUtc(string dir)
    {
        var created = Directory.GetCreationTimeUtc(dir);
        if (created < MinValidDate)
        {
            return Directory.GetLastWriteTimeUtc(dir);
        }
        return created;
    }

    /// <summary>
    /// Deletes session directories older than maxAge. Safe to call at startup.
    /// </summary>
    public static void CleanupStaleSessions(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(SessionsBaseDirectory))
                return;

            var cutoff = DateTime.UtcNow - maxAge;
            var dirs = Directory.GetDirectories(SessionsBaseDirectory);
            int cleaned = 0;

            foreach (var dir in dirs)
            {
                try
                {
                    var dirAge = GetDirectoryAgeUtc(dir);
                    if (dirAge < cutoff)
                    {
                        Directory.Delete(dir, recursive: true);
                        cleaned++;
                        Console.WriteLine($"[CLEANUP] Stale session removed: {Path.GetFileName(dir)} (age {dirAge:yyyy-MM-dd HH:mm} UTC)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLEANUP] Failed to remove stale dir {Path.GetFileName(dir)}: {ex.Message}");
                }
            }

            if (cleaned > 0)
                Console.WriteLine($"[CLEANUP] Removed {cleaned} stale session(s) at startup");
            else
                Console.WriteLine($"[CLEANUP] No stale sessions found (checked {dirs.Length} dir(s))");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEANUP] Startup cleanup error: {ex.Message}");
        }
    }
}
```

### Project Structure Notes

- `Services/` directory already exists from E0-S2 (NavigationService)
- `DeviceConfig.cs` at project root follows same pattern as `PhotoBooth.UI/DeviceConfig.cs`
- No .csproj changes needed — `PhotoBooth.Core` is already referenced (provides `Session`, `Layout`, etc.)
- `System.Text.Json` is built into .NET 10 — no extra package for SequentialNumberService

### References

- [Source: PhotoBooth.UI/Services/SessionService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Services/SessionService.cs) — original to copy from (280 lines)
- [Source: PhotoBooth.UI/Services/SequentialNumberService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Services/SequentialNumberService.cs) — copy with namespace change (137 lines)
- [Source: PhotoBooth.UI/DeviceConfig.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/DeviceConfig.cs) — reference for DeviceConfig stub
- [PhotoBooth.Core/Models/Session.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Core/Models/Session.cs) — Session model (shared via project reference)
- [PhotoBooth.Event/Services/NavigationService.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Services/NavigationService.cs) — E0-S2 pattern reference
- [E0-S2 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E0-S2-navigation-service.md) — previous story patterns
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E0-S3 entry line 43

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

None — clean implementation, no debugging needed.

### Completion Notes List

- ✅ Task 1: Created `SessionService.cs` (168 lines) from `PhotoBooth.UI` source (280 lines) — 40% reduction. Removed: `SetLayout`, `SetFrame`, `SetBackground`, `AddSticker`, `SetPaymentComplete`, `PreFetchDriveUrlAsync`, `_sessionCts` field, `GC.Collect` block, unused `using` directives (`System.Net.Http`, `System.Threading`, `System.Threading.Tasks`).
- ✅ Task 1: Created minimal `DeviceConfig.cs` stub with `GoogleDriveEnabled`, `GoogleDrivePath`, `GetMaskedGDrivePath()` — sufficient for SessionService, to be expanded in E0-S4.
- ✅ Task 2: Copied `SequentialNumberService.cs` (137 lines) with namespace change only. Counter file path intentionally shared (`~/.photobooth/counter.json`).
- ✅ Task 3: `dotnet build` succeeded with 0 errors (2 pre-existing NuGet vulnerability warnings for `Tmds.DBus.Protocol`).

### File List

- `src/PhotoBooth.Event/Services/SessionService.cs` (NEW)
- `src/PhotoBooth.Event/Services/SequentialNumberService.cs` (NEW)
- `src/PhotoBooth.Event/DeviceConfig.cs` (NEW)
- `_bmad-output/sprint-status.yaml` (MODIFIED — E0-S3 status: ready-for-dev → review → done)

## Senior Developer Review (AI)

**Reviewer:** Nguyenhuy — 2026-08-04
**Outcome:** ✅ Approved (all issues fixed)

### Findings Fixed (7 total: 1 High, 4 Medium, 2 Low)

| ID | Severity | Description | Fix |
|---|---|---|---|
| H1 | HIGH | `PeekNextNumber()` returned last-assigned number instead of next | Changed to `state.Counter + 1` |
| M1 | MEDIUM | `DeviceConfig` used fully-qualified `System.IO.Path` instead of `using` directive | Added `using System.IO;`, simplified references |
| M2 | MEDIUM | `SequentialNumberService` duplicated with no sync warning | Added sync warning comment at file top |
| M3 | MEDIUM | `DeviceConfig.GoogleDriveEnabled` defaults to `false` (UI defaults `true`) — undocumented | Added doc comment noting difference for E0-S4 |
| M4 | MEDIUM | Mixed Vietnamese/English comments | Translated all Vietnamese comments to English |
| L1 | LOW | Story estimate "~140 lines" vs actual 168 lines | Acknowledged — no code fix needed |
| L2 | LOW | Orphaned finding references (F1-F4, A1, Finding 6) | Removed all Fx:/Finding prefixes, kept explanations |

### AC Verification

All 15 Acceptance Criteria verified as IMPLEMENTED. Build succeeds with 0 errors.
