# Story 1.2: Start View UI

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want to see a visually clear welcome screen with a prominent start button,
so that I can intuitively begin a new photo session by tapping the screen.

## Acceptance Criteria

1. `StartView.axaml` exists at `src/PhotoBooth.Event/Views/StartView.axaml` as a `UserControl`
2. `StartView.axaml.cs` codebehind exists at `src/PhotoBooth.Event/Views/StartView.axaml.cs` — constructor calls `InitializeComponent()` only
3. XAML namespace: `x:Class="PhotoBooth.Event.Views.StartView"`, `x:DataType="vm:StartViewModel"`
4. View binds the start button to `{Binding StartCommand}` — the auto-generated RelayCommand from StartViewModel
5. ViewLocator resolves `StartViewModel` → `StartView` correctly (convention: replace `ViewModel` with `View` in full type name)
6. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors
7. App launches and displays the StartView on startup without crashing
8. This is a **placeholder** view — functional button binding only, visual design will be done later
9. **NO** device lock overlay (`IsDeviceLocked` border) — removed in E1-S1
10. **NO** background image references — `Assets/` directory is empty (`.gitkeep` only)
11. **NO** `ImageBtn` button style — no image assets exist for the Event project

## Tasks / Subtasks

- [x] Task 1: Create StartView.axaml (AC: 1, 3, 4, 8, 9, 10, 11)
  - [x] Create `src/PhotoBooth.Event/Views/StartView.axaml`
  - [x] Set `x:Class="PhotoBooth.Event.Views.StartView"`
  - [x] Set `x:DataType="vm:StartViewModel"` (compiled bindings)
  - [x] Add `xmlns:vm="using:PhotoBooth.Event.ViewModels"` namespace
  - [x] Add a `Button` with `Command="{Binding StartCommand}"`
  - [x] Use simple text-based placeholder layout (no image assets)
  - [x] Do NOT include `IsDeviceLocked` overlay
  - [x] Do NOT reference any assets from `Assets/backgrounds/` or `Assets/buttons/`
- [x] Task 2: Create StartView.axaml.cs codebehind (AC: 2)
  - [x] Create `src/PhotoBooth.Event/Views/StartView.axaml.cs`
  - [x] Namespace: `PhotoBooth.Event.Views`
  - [x] Class: `public partial class StartView : UserControl`
  - [x] Constructor: `InitializeComponent()` only — no logic
  - [x] Only `using Avalonia.Controls;` — no other usings needed
- [x] Task 3: Verify build (AC: 6)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors
- [x] Task 4: Verify ViewLocator resolution (AC: 5, 7)
  - [x] Confirm `ViewLocator.Build()` resolves `StartViewModel` → `StartView`
  - [x] Run the app to confirm StartView appears on startup


## Dev Notes

### Reuse Strategy: write_new

This is a **write_new** story — no source to copy from. The UI project's `StartView.axaml` is NOT reusable because:
- It references `Assets/backgrounds/back1.png` and `Assets/buttons/next.png` — neither exists in Event
- It has an `IsDeviceLocked` overlay binding — StartViewModel has no such property
- It uses a custom `ImageBtn` style with press/hover animations — overkill for a placeholder

### Critical: ViewLocator Convention

[ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs) resolves views by string replacement:

```csharp
var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
var type = Type.GetType(name);
```

This means:
- Input: `PhotoBooth.Event.ViewModels.StartViewModel`
- Output: `PhotoBooth.Event.Views.StartView`

**The View MUST be:**
- Namespace: `PhotoBooth.Event.Views`
- Class name: `StartView`
- If either is wrong, ViewLocator shows "Not Found: ..." text instead of the view

### Critical: Compiled Bindings

[PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj#L7) has `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`.

This means `x:DataType="vm:StartViewModel"` is **required** on the UserControl. Without it, `{Binding StartCommand}` will fail silently at compile time (no binding generated).

### Critical: StartCommand Binding Name

[StartViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/StartViewModel.cs#L26-L31) has `[RelayCommand]` on `private void Start()`. CommunityToolkit.Mvvm auto-generates:
- Property: `public IRelayCommand StartCommand { get; }`
- The XAML binding must be `{Binding StartCommand}` — NOT `{Binding Start}` or `{Binding StartAsync}`

### Critical: Design Dimensions

[MainWindow.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml#L6) uses `d:DesignWidth="1920" d:DesignHeight="1080"`. The StartView should use the same design dimensions for consistency. The MainWindow is `WindowState="Maximized"`.

### Critical: No Assets Available

The Event project's [Assets/](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Assets) directory contains only `.gitkeep`. Do NOT reference any image files. Use text-based content only for this placeholder.

### Placeholder Design Approach

Since this is "design sau" (design later), create a minimal but functional placeholder:
- Dark background matching MainWindow (`#1a1a2e`)
- Center-aligned welcome text
- Large, clearly visible start button with text label
- Use Avalonia's FluentTheme default button styling (already configured in [App.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/App.axaml#L13))
- **Touch-friendly sizing:** This is a kiosk/event app on touch screens — button must be large enough to tap comfortably. Minimum `Padding="60,20"` and `FontSize="32"` on the start button.

### Expected Final Code

**StartView.axaml:**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PhotoBooth.Event.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d" d:DesignWidth="1920" d:DesignHeight="1080"
             x:Class="PhotoBooth.Event.Views.StartView"
             x:DataType="vm:StartViewModel"
             Background="#1a1a2e">

    <StackPanel HorizontalAlignment="Center"
                VerticalAlignment="Center"
                Spacing="40">
        <TextBlock Text="PhotoBooth Event"
                   FontSize="48" FontWeight="Bold"
                   Foreground="White"
                   HorizontalAlignment="Center"/>
        <TextBlock Text="Tap to start your photo session"
                   FontSize="24" Foreground="#aaaaaa"
                   HorizontalAlignment="Center"/>
        <Button Content="START"
                Command="{Binding StartCommand}"
                FontSize="32" FontWeight="Bold"
                Padding="60,20"
                HorizontalAlignment="Center"
                Cursor="Hand"/>
    </StackPanel>

</UserControl>
```

**StartView.axaml.cs:**

```csharp
using Avalonia.Controls;

namespace PhotoBooth.Event.Views;

public partial class StartView : UserControl
{
    public StartView()
    {
        InitializeComponent();
    }
}
```

### Critical: Codebehind Using Statements

Only `using Avalonia.Controls;` is needed in the codebehind — for the `UserControl` base class. Do NOT add unnecessary usings. This matches the UI project's [StartView.axaml.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Views/StartView.axaml.cs) pattern exactly.

### What This Story Does NOT Do

- Does NOT add any image assets or complex styling
- Does NOT implement any animations or transitions
- Does NOT modify any existing files (StartViewModel, MainWindowViewModel, ViewLocator, etc.)
- Does NOT add any new NuGet packages
- Does NOT implement any ViewModel logic (that's E1-S1, already done)
- Does NOT create Views for other screens (CaptureView, PhotoSelectView, ReviewPrintView — those are E2-S2, E3-S2, E4 stories)

### Epic 0 Retrospective Action Items (Apply Here)

From [epic-0-retro](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md):
1. **All comments in English** — no Vietnamese text in XAML comments
2. **Verify logic correctness** — confirm binding names match exactly
3. **Defensive patterns** — N/A (no codebehind logic)

### Previous Story Intelligence (E1-S1)

From [E1-S1-start-viewmodel.md](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S1-start-viewmodel.md):
- StartViewModel is complete with `[RelayCommand] private void Start()` → generates `StartCommand`
- Constructor: `(NavigationService, SessionService)` — no changes needed
- Fields are `private readonly` (review fix from E1-S1 changed from `protected`)
- No `IsDeviceLocked` property exists — do NOT bind to it
- **XAML binding name confirmed:** `StartCommand` (see E1-S1 Dev Notes → "Critical: RelayCommand Source Generator")

### Project Structure Notes

- New files (2): `src/PhotoBooth.Event/Views/StartView.axaml`, `src/PhotoBooth.Event/Views/StartView.axaml.cs`
- No files modified or deleted
- Views directory already exists with `MainWindow.axaml` and `MainWindow.axaml.cs`
- Naming follows existing convention: `{ScreenName}View.axaml` + `.axaml.cs`

### References

- [ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs) — `ViewModel` → `View` name convention
- [StartViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/StartViewModel.cs) — `[RelayCommand]` on `Start()` → `StartCommand`
- [MainWindow.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml) — host ContentControl, design dimensions, dark background
- [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L47) — `NavigateTo<StartViewModel>()` on startup
- [App.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/App.axaml) — FluentTheme, ViewLocator registration
- [PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj#L7) — compiled bindings enabled
- [UI StartView.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Views/StartView.axaml) — reference ONLY, NOT reusable (uses missing assets + removed ViewModel props)
- [UI StartView.axaml.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Views/StartView.axaml.cs) — codebehind pattern reference (constructor + InitializeComponent only)
- [E1-S1 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S1-start-viewmodel.md) — previous story, established StartCommand binding
- [Epic 0 Retrospective](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md) — action items for write_new

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

- Build: `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — 0 errors, 2 pre-existing NuGet warnings (NU1903 Tmds.DBus.Protocol)
- App launch: `dotnet run --project src/PhotoBooth.Event/PhotoBooth.Event.csproj` — launched and displayed StartView without crash

### Completion Notes List

- ✅ Created StartView.axaml as a minimal placeholder with dark background (#1a1a2e), centered StackPanel, welcome text, and touch-friendly START button bound to `{Binding StartCommand}`
- ✅ Created StartView.axaml.cs with minimal codebehind — `InitializeComponent()` only, single using statement
- ✅ ViewLocator resolution confirmed: `PhotoBooth.Event.ViewModels.StartViewModel` → `PhotoBooth.Event.Views.StartView` (exact string match via `Replace("ViewModel", "View")`)
- ✅ Build succeeds with zero errors
- ✅ App launches and displays StartView on startup without crash
- ✅ All AC 1–11 satisfied — no image assets, no device lock overlay, no ImageBtn style, compiled bindings with x:DataType set
- ✅ No unit tests needed — codebehind has zero logic (constructor + InitializeComponent only), XAML is a static layout placeholder
- ✅ All comments in English per Epic 0 retrospective action item

### File List

- `src/PhotoBooth.Event/Views/StartView.axaml` — **NEW** — Placeholder StartView XAML
- `src/PhotoBooth.Event/Views/StartView.axaml.cs` — **NEW** — Minimal codebehind

## Senior Developer Review (AI)

**Reviewer:** Nguyenhuy — 2026-08-04
**Verdict:** ✅ Approved (all issues fixed)
**Issues Found:** 0 High, 3 Medium, 3 Low — all resolved

### Findings & Fixes Applied

| ID | Severity | Finding | Fix |
|---|---|---|---|
| M1 | MEDIUM | No `Design.DataContext` — XAML previewer gets no DataContext | Added `<Design.DataContext><vm:StartViewModel/></Design.DataContext>` matching MainWindow pattern |
| M2 | MEDIUM | `Cursor="Hand"` is dead code on touchscreen kiosk | Replaced with `IsDefault="True"` for keyboard accessibility |
| M3 | MEDIUM | Background `#1a1a2e` hardcoded in both StartView and MainWindow | Added TODO comment to extract to shared `StaticResource` when finalizing design |
| L1 | LOW | No keyboard accessibility — Button not auto-focused | Added `IsDefault="True"` so Enter key triggers Start |
| L2 | LOW | Story doesn't note future integration test intent | Documented: no unit tests needed; integration/UI tests deferred to design finalization |
| L3 | LOW | Trailing blank line at end of XAML | Removed |

### AC Re-validation Post-Fix

All 11 ACs remain satisfied. Build: 0 errors, 2 pre-existing NuGet warnings.

## Change Log

- 2026-08-04: Created StartView placeholder UI (XAML + codebehind) — 2 new files, 0 modified, 0 deleted
- 2026-08-04: Code review fixes — added Design.DataContext, IsDefault, TODO for color resource, removed Cursor="Hand" and trailing whitespace
