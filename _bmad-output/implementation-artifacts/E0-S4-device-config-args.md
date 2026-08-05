# Story 0.4: Device Config Args

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a full `DeviceConfig` in `PhotoBooth.Event` that parses command-line arguments for store, device, API, Google Drive, and printer settings,
so that the Event app can be launched from Admin with the same arg-passing pattern as `PhotoBooth.UI` — without layout/payment args.

## Acceptance Criteria

1. `src/PhotoBooth.Event/DeviceConfig.cs` exists with namespace `PhotoBooth.Event` — expanding the minimal stub from E0-S3
2. Static properties present: `StoreId` (int?), `DeviceId` (string), `PlanType` (string), `ApiBaseUrl` (string with TrimEnd('/')), `EnablePrinting` (bool), `PrinterName` (string), `GoogleDriveEnabled` (bool), `GoogleDrivePath` (string), `AppsScriptUrl` (string)
3. `PriceLayout6` and `PriceLayout2` are **NOT** present — Event has no payment
4. `GetMaskedGDrivePath()` method — identical to UI version (already in stub)
5. `GetMaskedAppsScriptUrl()` method — identical to UI version
6. `ParseArgs(string[] args)` method parses: `--storeId=`, `--deviceId=`, `--planType=`, `--apiBaseUrl=`, `--googleDriveEnabled=`, `--googleDrivePath=`, `--appsScriptUrl=`, `--enablePrinting=`, `--printerName=`
7. `ParseArgs` does **NOT** parse `--priceLayout6=` or `--priceLayout2=` — Event has no layout pricing
8. `ParseArgs` uses `Substring` (not `Replace`) to extract values — matches UI pattern (Finding 6 fix)
9. `ParseArgs` rejects empty `--deviceId=` values — matches UI pattern (Finding 9 fix)
10. `ParseArgs` logs all config values at end using masked GDrive/AppsScript — without PriceLayout fields
11. `Program.cs` calls `DeviceConfig.ParseArgs(args)` after exception handlers — so malformed args crash is logged (improvement over UI placement)
12. `Program.cs` calls `SessionService.CleanupStaleSessions(TimeSpan.FromHours(72))` after ParseArgs — same as UI
13. `GoogleDriveEnabled` defaults to `true` — matching UI default (E0-S3 stub had `false`, noted for correction)
14. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors

## Tasks / Subtasks

- [x] Task 1: Expand DeviceConfig.cs from stub to full (AC: 1-10, 13)
  - [x] Open existing `src/PhotoBooth.Event/DeviceConfig.cs` (26 lines from E0-S3)
  - [x] Add `using System;` directive (stub only has `using System.IO;` — needed for `Uri`, `Console`, `TimeSpan`)
  - [x] Add properties: `StoreId`, `DeviceId`, `PlanType` (keep `// "Basic" or "Pro"` comment), `ApiBaseUrl` (with TrimEnd), `EnablePrinting`, `PrinterName`, `AppsScriptUrl`
  - [x] Change `GoogleDriveEnabled` default from `false` to `true`
  - [x] Add `GetMaskedAppsScriptUrl()` method
  - [x] Add `ParseArgs(string[] args)` method — copy from UI, remove `--priceLayout6=` and `--priceLayout2=` blocks
  - [x] Update log line in ParseArgs — remove PriceLayout6/PriceLayout2 fields
  - [x] Update doc comment — remove "stub" references, describe full purpose
- [x] Task 2: Wire ParseArgs + CleanupStaleSessions in Program.cs (AC: 11-12)
  - [x] Add `DeviceConfig.ParseArgs(args)` after exception handlers block, before CleanupStaleSessions
  - [x] Add `using PhotoBooth.Event.Services;` at the top (needed for bare `SessionService` call)
  - [x] Add `SessionService.CleanupStaleSessions(TimeSpan.FromHours(72))` after exception handlers, before memory monitor — use bare name (NOT `Services.SessionService`), matching UI pattern
- [x] Task 3: Verify build (AC: 14)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: copy_and_simplify

Source: [DeviceConfig.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/DeviceConfig.cs) (153 lines → ~120 lines target)

**KEEP** (all device/store/API/GDrive/printer config):

| Property/Method | Default | Notes |
|---|---|---|
| `StoreId` | `null` (int?) | Admin passes via `--storeId=` |
| `DeviceId` | `"device-1"` | Admin passes via `--deviceId=` |
| `PlanType` | `"Pro"` | Admin passes via `--planType=` |
| `ApiBaseUrl` | `"http://localhost:5148"` | Setter does `TrimEnd('/')` |
| `EnablePrinting` | `false` | Admin passes via `--enablePrinting=` |
| `PrinterName` | `""` | Admin passes via `--printerName=` |
| `GoogleDriveEnabled` | `true` | ⚠️ Change from stub default `false` |
| `GoogleDrivePath` | `""` | Admin passes via `--googleDrivePath=` |
| `AppsScriptUrl` | `""` | Admin passes via `--appsScriptUrl=` |
| `GetMaskedGDrivePath()` | — | Already in stub |
| `GetMaskedAppsScriptUrl()` | — | Copy from UI |
| `ParseArgs(string[])` | — | Copy from UI, remove 2 blocks |

**REMOVE** (layout/payment pricing — Event hardcodes 1 layout, no payment):

| Property | Reason |
|---|---|
| `PriceLayout6` | Event has no payment |
| `PriceLayout2` | Event has no payment |
| `--priceLayout6=` arg | Event has no payment |
| `--priceLayout2=` arg | Event has no payment |

### Critical: GoogleDriveEnabled Default

The E0-S3 stub set `GoogleDriveEnabled = false` with a doc comment noting this difference. The UI version defaults to `true`. This story MUST change the default to `true` to match the UI, since the Admin launcher sends `--googleDriveEnabled=` args and the default should be the same for consistency.

### Critical: Program.cs Integration

The UI's `Program.cs` calls `DeviceConfig.ParseArgs(args)` *before* exception handlers. The Event version intentionally places it *after* exception handlers so that malformed args produce a `[CRASH]` log line instead of a silent termination. The Event's `Program.cs` currently does NOT call ParseArgs. This story adds (requires `using PhotoBooth.Event.Services;` at top):

```diff
+using PhotoBooth.Event.Services;
 ...
         // ──────────────────────────────────────────────────────────────────────────
 
+        DeviceConfig.ParseArgs(args);
+
+        // Cleanup session folders older than 72 hours to prevent disk exhaustion
+        // during long-running kiosk operation.
+        SessionService.CleanupStaleSessions(TimeSpan.FromHours(72));
+
         // Background memory monitoring — logs every 60s for diagnosing leaks during 12h operation
```

### Critical: ParseArgs Log Line

The UI log line includes `PriceLayout6` and `PriceLayout2`. The Event version must omit these:

```csharp
// UI version (with layout pricing):
Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GetMaskedGDrivePath()}, AppsScriptUrl={GetMaskedAppsScriptUrl()}, EnablePrinting={EnablePrinting}, PrinterName={PrinterName}");

// Event version (without layout pricing):
Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GetMaskedGDrivePath()}, AppsScriptUrl={GetMaskedAppsScriptUrl()}, EnablePrinting={EnablePrinting}, PrinterName={PrinterName}");
```

### Critical: Substring Pattern (Not Replace)

The UI's `ParseArgs` uses `arg.Substring("--prefix=".Length)` instead of `arg.Replace("--prefix=", "")`. This is intentional (Finding 6 from UI code review) — `Replace` would strip the prefix twice if the value contained the prefix string. **Copy this pattern exactly.**

### Critical: Empty DeviceId Rejection

The UI rejects empty `--deviceId=` values (Finding 9 from UI code review):
```csharp
var val = arg.Substring("--deviceId=".Length);
if (!string.IsNullOrWhiteSpace(val))
    DeviceId = val;
```
**Copy this pattern exactly.** Same whitespace guard applies to `--googleDrivePath=`, `--appsScriptUrl=`, `--printerName=`.

### Vietnamese Comments

The UI source has Vietnamese comments in the Google Drive section. **Translate all to English** for the Event version (matching pattern from E0-S3 code review finding M4).

### `using System;` Convention (Deviation from UI Source)

The UI source uses fully-qualified `System.Uri(...)` and `System.Console.WriteLine(...)` without `using System;`. The Event project convention is `using System;` (all existing `.cs` files use it). **Use `using System;` + bare `Uri`/`Console`** — do NOT copy the UI's fully-qualified style.

### Expected Final Code — DeviceConfig.cs

```csharp
using System;
using System.IO;

namespace PhotoBooth.Event;

/// <summary>
/// Stores device info passed from Admin app login (via command-line args).
/// Simplified for Event flow — no layout pricing (PriceLayout6/PriceLayout2).
/// </summary>
public static class DeviceConfig
{
    public static int? StoreId { get; set; }
    public static string DeviceId { get; set; } = "device-1";
    public static string PlanType { get; set; } = "Pro";  // "Basic" or "Pro"
    private static string _apiBaseUrl = "http://localhost:5148";
    public static string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => _apiBaseUrl = value?.TrimEnd('/') ?? "";
    }

    public static bool EnablePrinting { get; set; } = false;
    public static string PrinterName { get; set; } = "";

    // Google Drive configuration
    public static bool GoogleDriveEnabled { get; set; } = true;
    public static string GoogleDrivePath { get; set; } = "";
    public static string AppsScriptUrl { get; set; } = "";

    /// <summary>
    /// Returns GoogleDrivePath with PII masked — shows only last 2 path segments.
    /// Use this in all log output to avoid leaking user email from Google Drive paths.
    /// </summary>
    public static string GetMaskedGDrivePath()
    {
        if (string.IsNullOrEmpty(GoogleDrivePath)) return "";
        var segments = GoogleDrivePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Length < 2) return ".../";
        return ".../" + string.Join("/", segments[^2..]);
    }

    /// <summary>
    /// Returns AppsScriptUrl with deployment key masked — shows only domain + first 10 chars.
    /// Deployment keys (AKfycb...) are secrets that should not appear in logs.
    /// </summary>
    public static string GetMaskedAppsScriptUrl()
    {
        if (string.IsNullOrEmpty(AppsScriptUrl)) return "";
        try
        {
            var uri = new Uri(AppsScriptUrl);
            var path = uri.AbsolutePath;
            var masked = path.Length > 10 ? path[..10] + "..." : path;
            return $"{uri.Scheme}://{uri.Host}{masked}";
        }
        catch
        {
            return "<invalid-url>";
        }
    }

    public static void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--storeId="))
            {
                if (int.TryParse(arg.Substring("--storeId=".Length), out var storeId))
                    StoreId = storeId;
            }
            else if (arg.StartsWith("--deviceId="))
            {
                var val = arg.Substring("--deviceId=".Length);
                if (!string.IsNullOrWhiteSpace(val))
                    DeviceId = val;
            }
            else if (arg.StartsWith("--planType="))
            {
                PlanType = arg.Substring("--planType=".Length);
            }
            else if (arg.StartsWith("--apiBaseUrl="))
            {
                ApiBaseUrl = arg.Substring("--apiBaseUrl=".Length);
            }
            // NOTE: --priceLayout6= and --priceLayout2= intentionally NOT parsed
            // Event flow has no payment — layout pricing is irrelevant
            else if (arg.StartsWith("--googleDriveEnabled="))
            {
                GoogleDriveEnabled = bool.TryParse(arg.Substring("--googleDriveEnabled=".Length), out var gde) && gde;
            }
            else if (arg.StartsWith("--googleDrivePath="))
            {
                var path = arg.Substring("--googleDrivePath=".Length);
                if (!string.IsNullOrWhiteSpace(path))
                    GoogleDrivePath = path;
            }
            else if (arg.StartsWith("--appsScriptUrl="))
            {
                var url = arg.Substring("--appsScriptUrl=".Length);
                if (!string.IsNullOrWhiteSpace(url))
                    AppsScriptUrl = url;
            }
            else if (arg.StartsWith("--enablePrinting="))
            {
                EnablePrinting = bool.TryParse(arg.Substring("--enablePrinting=".Length), out var ep) && ep;
            }
            else if (arg.StartsWith("--printerName="))
            {
                var name = arg.Substring("--printerName=".Length);
                if (!string.IsNullOrWhiteSpace(name))
                    PrinterName = name;
            }
        }

        Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GetMaskedGDrivePath()}, AppsScriptUrl={GetMaskedAppsScriptUrl()}, EnablePrinting={EnablePrinting}, PrinterName={PrinterName}");
    }
}
```

### Project Structure Notes

- `DeviceConfig.cs` stays at project root `src/PhotoBooth.Event/DeviceConfig.cs` — same location as `PhotoBooth.UI/DeviceConfig.cs`
- No new files — this story expands an existing stub
- No .csproj changes — no new dependencies needed (`System`, `System.IO` are built-in)
- `Program.cs` modified in-place (2 additions)

### What This Story Does NOT Do

- Does NOT create `HttpService` — separate story
- Does NOT add layout/payment properties — intentionally removed for Event
- Does NOT modify `SessionService` — already done in E0-S3
- Does NOT modify `MainWindowViewModel` — that's E0-S5

### Files to Modify

```
src/PhotoBooth.Event/
├── DeviceConfig.cs    (MODIFY — expand from 26-line stub to ~120 lines)
└── Program.cs         (MODIFY — add ParseArgs + CleanupStaleSessions calls)
```

### References

- [Source: PhotoBooth.UI/DeviceConfig.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/DeviceConfig.cs) — original to copy from (153 lines)
- [Current: PhotoBooth.Event/DeviceConfig.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/DeviceConfig.cs) — E0-S3 stub to expand (26 lines)
- [PhotoBooth.UI/Program.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Program.cs) — reference for ParseArgs + CleanupStaleSessions wiring
- [PhotoBooth.Event/Program.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Program.cs) — file to modify
- [E0-S3 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E0-S3-session-service-simplified.md) — previous story patterns, DeviceConfig stub origin
- [sprint-status.yaml](file:///Users/nguyenhuy/works/ptb/_bmad-output/sprint-status.yaml) — E0-S4 entry line 45

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

No issues encountered during implementation.

### Completion Notes List

- Expanded DeviceConfig.cs from 26-line stub to 118-line full implementation
- Copied all properties/methods from PhotoBooth.UI/DeviceConfig.cs, removed PriceLayout6/PriceLayout2 (Event has no payment)
- Changed GoogleDriveEnabled default from `false` to `true` (matching UI)
- Added GetMaskedAppsScriptUrl() method using `using System;` + bare `Uri` (Event convention, not UI's fully-qualified style)
- Added ParseArgs() with Substring pattern (not Replace) per Finding 6; empty DeviceId rejection per Finding 9
- Translated Vietnamese comments to English
- Wired ParseArgs(args) in Program.cs after exception handlers (intentional placement for crash logging)
- Wired SessionService.CleanupStaleSessions(TimeSpan.FromHours(72)) after ParseArgs
- Build succeeded with 0 errors, 2 pre-existing NuGet advisory warnings (unrelated)
- All 14 acceptance criteria verified ✅

### File List

- `src/PhotoBooth.Event/DeviceConfig.cs` (MODIFIED — expanded from 26 to 118 lines)
- `src/PhotoBooth.Event/Program.cs` (MODIFIED — added ParseArgs + CleanupStaleSessions calls)

### Change Log

- 2026-08-04: Expanded DeviceConfig from stub to full arg-parsing implementation; wired ParseArgs + CleanupStaleSessions in Program.cs
- 2026-08-04: Code review — fixed 3 MEDIUM + 3 LOW issues: added empty-value guards for --planType= and --apiBaseUrl=, added warning log for malformed --storeId, explicit catch type in GetMaskedAppsScriptUrl, split long CONFIG log line for readability

### Senior Developer Review (AI)

**Reviewer:** Nguyenhuy — 2026-08-04
**Outcome:** Approved (all issues auto-fixed)

| ID | Severity | Description | Status |
|---|---|---|---|
| M1 | MEDIUM | `--planType=` accepts empty values, losing default | ✅ Fixed — added IsNullOrWhiteSpace guard |
| M2 | MEDIUM | `--apiBaseUrl=` accepts empty values, kills HTTP | ✅ Fixed — added IsNullOrWhiteSpace guard |
| M3 | MEDIUM | `--storeId=abc` silently ignored — no warning | ✅ Fixed — added Console.WriteLine warning |
| L1 | LOW | `using var process` in memory monitor (better than UI) | ✅ No change needed — already correct |
| L2 | LOW | Bare `catch` in GetMaskedAppsScriptUrl | ✅ Fixed — changed to `catch (Exception)` |
| L3 | LOW | CONFIG log line 250+ chars | ✅ Fixed — split across 4 concatenated lines |
| L4 | LOW | sprint-status.yaml says "--layoutId" but actual is "--priceLayout6" | ⚠️ Documentation-only mismatch, sprint comment is a summary |
