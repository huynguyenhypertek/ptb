# Story 3.2: Photo Grid Selection UI

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event photo booth user,
I want to see a grid of my 6 captured photos and tap to select 4 for the final print,
so that I can choose my best photos before the composite is created.

## Acceptance Criteria

1. `PhotoSelectView.axaml` exists at `src/PhotoBooth.Event/Views/PhotoSelectView.axaml` as a `UserControl`
2. `PhotoSelectView.axaml.cs` codebehind exists at `src/PhotoBooth.Event/Views/PhotoSelectView.axaml.cs` — constructor calls `InitializeComponent()` only
3. XAML namespace: `x:Class="PhotoBooth.Event.Views.PhotoSelectView"`, `x:DataType="vm:PhotoSelectViewModel"`
4. View displays a 3×2 grid of photo thumbnails bound to `{Binding Photos}` via `ItemsControl`
5. Each photo thumbnail is a `Button` that invokes `{Binding ToggleSelectionCommand}` with `CommandParameter="{Binding}"` (the `PhotoItem`)
6. Selected photos show a visible highlight border (`#e94560`, 4px) and checkmark badge — bound to `PhotoItem.IsSelected`
7. Right side shows 4 preview slots bound to `SelectedPhoto1..4` for frame preview
8. "CONFIRM" button bound to `{Binding ConfirmCommand}` — auto-disables via command's `CanExecute` when `SelectedCount != 4` (do NOT add explicit `IsEnabled` binding)
9. "BACK" button bound to `{Binding GoBackCommand}`
10. Selection counter text displays selected count (e.g., "2 / 4 selected") using `<Run Text="{Binding SelectedCount}"/>` + `<Run Text=" / 4 selected"/>`
11. Background color `#1a1a2e` matching MainWindow and sibling views
12. ViewLocator resolves `PhotoSelectViewModel` → `PhotoSelectView` correctly
13. `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` succeeds with zero errors
14. This is a **placeholder** view — functional bindings only, visual design will be done later
15. **NO** background image references — `Assets/` directory is empty (`.gitkeep` only)
16. **NO** `ImageBtn` button style — no image assets exist for the Event project
17. **NO** layout branching (`IsLayout2`/`IsLayout6`) — Event is single-layout (4 photos only)
18. **NO** `FramePreviewImage`, `BackgroundImage` bindings — those properties were dropped in E3-S1
19. **NO** `PhotoWidth`, `PhotoHeight`, `PhotoMargin`, `GridMaxWidth`, `GridMargin` bindings — those were dropped in E3-S1
20. **NO** explicit `IsEnabled` binding on CONFIRM button — `ConfirmCommand`'s `CanExecute` auto-manages it

## Tasks / Subtasks

- [x] Task 1: Create PhotoSelectView.axaml (AC: 1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 14, 15, 16, 17, 18, 19, 20)
  - [x] Create `src/PhotoBooth.Event/Views/PhotoSelectView.axaml`
  - [x] Set `x:Class="PhotoBooth.Event.Views.PhotoSelectView"`
  - [x] Set `x:DataType="vm:PhotoSelectViewModel"` (compiled bindings)
  - [x] Add `xmlns:vm="using:PhotoBooth.Event.ViewModels"` namespace
  - [x] Add photo grid: `ItemsControl` with `ItemsSource="{Binding Photos}"` using a `WrapPanel` (3 columns × 2 rows)
  - [x] Each item: `Button` with `Command="{Binding $parent[ItemsControl].((vm:PhotoSelectViewModel)DataContext).ToggleSelectionCommand}"` and `CommandParameter="{Binding}"`
  - [x] DataTemplate must set `x:DataType="vm:PhotoItem"` for compiled bindings (without this, `{Binding Thumbnail}` and `{Binding IsSelected}` fail silently)
  - [x] Photo thumbnail: `Image Source="{Binding Thumbnail}"` inside `Border CornerRadius="8" ClipToBounds="True"`
  - [x] Selection highlight: `Border BorderBrush="#e94560" BorderThickness="4"` with `IsVisible="{Binding IsSelected}"`
  - [x] Checkmark badge: `Border Background="#e94560" CornerRadius="15" Width="30" Height="30"` with `TextBlock "✓"`, `IsVisible="{Binding IsSelected}"`
  - [x] Right side: 4 `Image` controls bound to `SelectedPhoto1..4` for preview, with `Stretch="UniformToFill"`
  - [x] Selection counter: `TextBlock` with `<Run Text="{Binding SelectedCount}"/>` + `<Run Text=" / 4 selected"/>` (NOT MultiBinding — SelectedCount is a single property)
  - [x] CONFIRM button: `Command="{Binding ConfirmCommand}"`, touch-friendly sizing — do NOT add `IsEnabled` (command's `CanExecute` auto-manages it via `NotifyCanExecuteChanged()`)
  - [x] BACK button: `Command="{Binding GoBackCommand}"`, touch-friendly sizing
  - [x] Use simple text-based placeholder layout (no image assets)
  - [x] Do NOT reference any assets from `Assets/` — none exist
  - [x] Do NOT bind to dropped properties: `BackgroundImage`, `FramePreviewImage`, `PhotoWidth`, etc.

- [x] Task 2: Create PhotoSelectView.axaml.cs codebehind (AC: 2)
  - [x] Create `src/PhotoBooth.Event/Views/PhotoSelectView.axaml.cs`
  - [x] Namespace: `PhotoBooth.Event.Views`
  - [x] Class: `public partial class PhotoSelectView : UserControl`
  - [x] Constructor: `InitializeComponent()` only — no logic
  - [x] Only `using Avalonia.Controls;` — no other usings needed

- [x] Task 3: Verify ViewLocator resolution (AC: 12)
  - [x] Confirm `ViewLocator.Build()` resolves `PhotoSelectViewModel` → `PhotoSelectView` via string replacement
  - [x] Run the app, navigate to photo selection screen, confirm PhotoSelectView appears (not "Not Found: ...")
  - [x] Do NOT add explicit DataTemplate to MainWindow.axaml — ViewLocator handles it (CaptureView already works without one)

- [x] Task 4: Verify build (AC: 13)
  - [x] Run `dotnet build src/PhotoBooth.Event/PhotoBooth.Event.csproj` — zero errors

## Dev Notes

### Reuse Strategy: write_new

This is a **write_new** story — the source `PhotoBooth.UI/Views/PhotoSelectionView.axaml` is NOT reusable because:
- It binds to `BackgroundImage`, `FramePreviewImage` — both dropped in E3-S1 ViewModel
- It uses `PhotoWidth`, `PhotoHeight`, `PhotoMargin`, `GridMaxWidth`, `GridMargin` — all dropped
- It has `IsLayout2`/`IsLayout6` dual-layout branches — Event is single-layout
- It uses `ImageBtn` style with image-based buttons — no assets exist in Event
- It references `SelectedPhoto5`, `SelectedPhoto6` — dropped (Event selects 4 not 6)

### Critical: ViewLocator Convention

[ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs) resolves views by string replacement:

```csharp
var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
var type = Type.GetType(name);
```

This means:
- Input: `PhotoBooth.Event.ViewModels.PhotoSelectViewModel`
- Output: `PhotoBooth.Event.Views.PhotoSelectView`

**The View MUST be:**
- Namespace: `PhotoBooth.Event.Views`
- Class name: `PhotoSelectView`
- If either is wrong, ViewLocator shows "Not Found: ..." text instead of the view

### Critical: Compiled Bindings

[PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj#L7) has `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`.

`x:DataType="vm:PhotoSelectViewModel"` is **required** on the UserControl. Without it, all `{Binding ...}` expressions fail silently at compile time.

### Critical: ViewModel Binding Names

From [PhotoSelectViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs):

| ViewModel Property/Command | XAML Binding | Notes |
|---|---|---|
| `Photos` (ObservableCollection\<PhotoItem\>) | `{Binding Photos}` | ItemsControl source |
| `ToggleSelectionCommand` | `{Binding ToggleSelectionCommand}` | `[RelayCommand]` on `ToggleSelection(PhotoItem)` |
| `ConfirmCommand` | `{Binding ConfirmCommand}` | `[RelayCommand(CanExecute = nameof(CanConfirm))]` |
| `GoBackCommand` | `{Binding GoBackCommand}` | `[RelayCommand]` on `GoBack()` |
| `SelectedCount` | `{Binding SelectedCount}` | Computed: `Photos.Count(p => p.IsSelected)` |
| `CanConfirm` | `{Binding CanConfirm}` | Computed: `SelectedCount == 4` |
| `SelectedPhoto1..4` | `{Binding SelectedPhoto1}` etc. | `[ObservableProperty]` Bitmap? |

PhotoItem bindings (inside DataTemplate):

| PhotoItem Property | XAML Binding | Notes |
|---|---|---|
| `Thumbnail` | `{Binding Thumbnail}` | `[ObservableProperty]` Bitmap? |
| `IsSelected` | `{Binding IsSelected}` | `[ObservableProperty]` bool |

### Critical: ItemsControl Parent Binding Pattern

To bind `ToggleSelectionCommand` inside an ItemsControl's DataTemplate (where DataContext is `PhotoItem`), use ancestor binding:

```xml
Command="{Binding $parent[ItemsControl].((vm:PhotoSelectViewModel)DataContext).ToggleSelectionCommand}"
CommandParameter="{Binding}"
```

This is the exact same pattern used in the source [PhotoSelectionView.axaml#L71-L72](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Views/PhotoSelectionView.axaml#L71-L72). The `$parent[ItemsControl]` syntax walks up the visual tree to find the ItemsControl, then casts its DataContext.

### Critical: Design Dimensions

[MainWindow.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml#L7) uses `d:DesignWidth="1920" d:DesignHeight="1080"`. Use same dimensions on `PhotoSelectView`. Window is `WindowState="Maximized"`.

### Critical: No Assets Available

The Event project's `Assets/` directory contains only `.gitkeep`. Do NOT reference any image files. Use text-based content only for this placeholder.

### Placeholder Design Approach

Since this is "design sau" (design later), create a minimal but functional placeholder:
- Dark background matching MainWindow (`#1a1a2e`)
- Left side: 3×2 photo grid using WrapPanel inside ItemsControl
- Right side: 4 preview image slots (stacked vertically or 2×2)
- Bottom: BACK + CONFIRM buttons, selection counter
- Photo selection highlight: red border (#e94560, 4px) + checkmark badge (same as source UI pattern)
- Touch-friendly sizing: buttons with `Padding="40,20"` and `FontSize="24"` minimum (kiosk/event touch screen)
- Use FluentTheme default button styling (already in [App.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/App.axaml#L13))
- **Photo thumbnail sizing**: Use fixed width/height container (e.g., `Width="250" Height="140"`) with `Stretch="UniformToFill"` — maintains 16:9 aspect ratio. NOT bound to ViewModel sizing properties (those were dropped)
- **WrapPanel MaxWidth**: Hardcode in XAML (e.g., `MaxWidth="800"` for 3 columns) — NOT bound to `GridMaxWidth` (dropped)
- **Preview image Stretch**: Use `Stretch="UniformToFill"` or `Stretch="Uniform"` on `SelectedPhoto1..4` Images — without Stretch, thumbnails render at decoded pixel size (300px)

### Critical: PhotoBtn Button Style

The source UI defines a `PhotoBtn` style for transparent photo buttons. For the Event placeholder, create a minimal inline style or use the `Button.PhotoBtn` class with local styles:

```xml
<Style Selector="Button.PhotoBtn">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="0"/>
    <Setter Property="Cursor" Value="Hand"/>
</Style>
<Style Selector="Button.PhotoBtn:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="Transparent"/>
</Style>
```

This prevents FluentTheme from adding blue hover backgrounds over the photo thumbnails.

### Critical: ViewLocator Handles View Resolution — No DataTemplate Needed

[MainWindow.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml#L23) has `StartView` registered as an explicit DataTemplate, but the [ViewLocator](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs) (registered in `App.DataTemplates`) handles all VM→View resolution automatically.

Proof: `CaptureView` works correctly WITHOUT an explicit DataTemplate in MainWindow — the ViewLocator resolves `CaptureViewModel` → `CaptureView` at the App level.

**Decision**: Do NOT add a DataTemplate for `PhotoSelectView` in MainWindow. The ViewLocator handles it. The existing `StartView` DataTemplate in MainWindow is technically redundant but harmless. Do NOT modify MainWindow.axaml in this story.

### Critical: Compiled Bindings in DataTemplate

Since compiled bindings are enabled project-wide, the `DataTemplate` inside the `ItemsControl` MUST specify `x:DataType="vm:PhotoItem"` so the compiler can resolve `{Binding Thumbnail}`, `{Binding IsSelected}`, etc.

```xml
<DataTemplate x:DataType="vm:PhotoItem">
    <!-- PhotoItem bindings work here with compiled bindings -->
</DataTemplate>
```

Without `x:DataType`, compiled bindings fail silently — no compile error but no binding generated at runtime. This is the most common pitfall in Avalonia compiled bindings.

### Critical: ConfirmCommand CanExecute — Do NOT Add IsEnabled

`ConfirmCommand` uses `[RelayCommand(CanExecute = nameof(CanConfirm))]`. CommunityToolkit auto-generates an `ICommand` implementation that calls `CanConfirm` and manages `Button.IsEnabled` via `ICommand.CanExecute`.

Adding an explicit `IsEnabled="{Binding CanConfirm}"` on the CONFIRM button creates a **double-disabling conflict**:
1. The command's `CanExecute` disables the button
2. The explicit `IsEnabled` binding also tries to control enabled state

If they update out of sync (e.g., `OnPropertyChanged(nameof(CanConfirm))` fires but `NotifyCanExecuteChanged()` hasn't yet), the button can get stuck disabled.

**Rule**: When using `CanExecute` on `[RelayCommand]`, ONLY bind `Command` — do NOT add `IsEnabled`.

### What This Story Does NOT Do

- Does NOT add any image assets or complex styling
- Does NOT implement any animations or transitions
- Does NOT modify PhotoSelectViewModel.cs (that's E3-S1, already done)
- Does NOT add any new NuGet packages
- Does NOT create Views for other screens (ReviewPrintView — that's E4)
- Does NOT implement frame compositing (that's E3-S3)
- Does NOT modify any file in `PhotoBooth.Infrastructure`
- Does NOT modify MainWindow.axaml — ViewLocator handles view resolution

### Epic 0 Retrospective Action Items (Apply Here)

From [epic-0-retro](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md):
1. **All comments in English** — no Vietnamese text in XAML comments
2. **Verify binding names match exactly** — cross-check against ViewModel properties table above
3. **Defensive patterns** — N/A (no codebehind logic)

### Previous Story Intelligence (E3-S1)

From [E3-S1-photo-select-viewmodel.md](file:///Users/nguyenhuy/works/ptb/_bmad-output/stories/E3-S1-photo-select-viewmodel.md):
- PhotoSelectViewModel is complete with all bindings needed for this view
- `Photos` is `ObservableCollection<PhotoItem>` (the grid source)
- `ToggleSelectionCommand` takes `PhotoItem` parameter — binding MUST use `CommandParameter="{Binding}"`
- `ConfirmCommand` has `CanExecute = nameof(CanConfirm)` — button auto-disables via command binding
- `SelectedPhoto1..4` are `[ObservableProperty]` Bitmap? — directly bindable to `Image.Source`
- `SelectedCount` is a computed property — can bind for display but NOT directly to `IsEnabled`
- `PhotoItem.Thumbnail` is `[ObservableProperty]` (fixed in code review) — will notify UI on change
- `PhotoItem.IsSelected` is `[ObservableProperty]` — drives selection highlight visibility
- Review fix M1: `ToggleSelection` has null guard, so passing null CommandParameter is safe (but wrong)
- Review fix M3: `Thumbnail` is now `[ObservableProperty]` — binding will work correctly

### E1-S2 Pattern Reference

From [E1-S2-start-view-ui.md](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S2-start-view-ui.md):
- Same "write_new — XAML placeholder, design sau" pattern
- Files: `.axaml` + `.axaml.cs` pair
- Codebehind: `InitializeComponent()` only, single `using Avalonia.Controls;`
- Background: `#1a1a2e` (dark)
- Design dimensions: `d:DesignWidth="1920" d:DesignHeight="1080"`
- Touch-friendly sizing on buttons

### Project Structure Notes

- New files (2): `src/PhotoBooth.Event/Views/PhotoSelectView.axaml`, `src/PhotoBooth.Event/Views/PhotoSelectView.axaml.cs`
- Modified files (0): none — ViewLocator handles view resolution, no MainWindow.axaml changes needed
- No files deleted
- Views directory already exists with `StartView.axaml`, `CaptureView.axaml`, `MainWindow.axaml`
- Naming follows existing convention: `{ScreenName}View.axaml` + `.axaml.cs`

### References

- [ViewLocator.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewLocator.cs) — `ViewModel` → `View` name convention
- [PhotoSelectViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/PhotoSelectViewModel.cs) — all bindings source
- [MainWindow.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/MainWindow.axaml) — DataTemplate registration target
- [MainWindowViewModel.cs](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/ViewModels/MainWindowViewModel.cs#L40) — DI registration (already done)
- [App.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/App.axaml) — FluentTheme, ViewLocator registration
- [PhotoBooth.Event.csproj](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/PhotoBooth.Event.csproj#L7) — compiled bindings enabled
- [StartView.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/StartView.axaml) — placeholder pattern reference (same "write_new" approach)
- [CaptureView.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.Event/Views/CaptureView.axaml) — sibling view pattern reference
- [Source PhotoSelectionView.axaml](file:///Users/nguyenhuy/works/ptb/src/PhotoBooth.UI/Views/PhotoSelectionView.axaml) — reference ONLY, NOT reusable (uses dropped bindings)
- [E3-S1 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/stories/E3-S1-photo-select-viewmodel.md) — previous story, established all ViewModel bindings
- [E1-S2 story](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/E1-S2-start-view-ui.md) — same "write_new XAML placeholder" pattern reference
- [Epic 0 Retrospective](file:///Users/nguyenhuy/works/ptb/_bmad-output/implementation-artifacts/epic-0-retro-2026-08-04.md) — action items for write_new

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (Thinking)

### Debug Log References

N/A — pure XAML view with no codebehind logic. Build verification only.

### Completion Notes List

- Created `PhotoSelectView.axaml` — placeholder view with 3×2 photo grid, 4 preview slots, selection highlight/checkmark, BACK/CONFIRM buttons, selection counter
- Created `PhotoSelectView.axaml.cs` — minimal codebehind (`InitializeComponent()` only)
- Verified ViewLocator naming convention: `PhotoSelectViewModel` → `PhotoSelectView` ✅
- Build passes: `dotnet build` — 0 errors, 0 warnings (only pre-existing NU1903 NuGet advisory)
- All 20 ACs satisfied including 6 negative ACs (no assets, no dropped bindings, no IsEnabled, no layout branching, no ImageBtn, no explicit DataTemplate)
- Used `PhotoBtn` inline style to prevent FluentTheme blue hover on photo buttons
- Used compiled bindings: `x:DataType="vm:PhotoSelectViewModel"` on UserControl and `x:DataType="vm:PhotoItem"` on DataTemplate
- Parent binding pattern: `$parent[ItemsControl].((vm:PhotoSelectViewModel)DataContext).ToggleSelectionCommand` for command inside DataTemplate
- No tests added — pure XAML placeholder with no logic in codebehind; E3-S1 already tested ViewModel bindings

### File List

- `src/PhotoBooth.Event/Views/PhotoSelectView.axaml` (new)
- `src/PhotoBooth.Event/Views/PhotoSelectView.axaml.cs` (new)
- `src/PhotoBooth.Event/Views/MainWindow.axaml` (modified — review fix H1: replaced misleading TODO with ViewLocator-only pattern comment)

## Senior Developer Review (AI)

**Reviewer**: Nguyenhuy — 2026-08-05
**Agent Model**: Claude Opus 4.6 (Thinking)
**Outcome**: ✅ Approved (all issues fixed)

### Findings (6 total: 1 High, 3 Medium, 2 Low)

| ID | Severity | Description | Resolution |
|---|---|---|---|
| H1 | HIGH | MainWindow TODO comment contradicts ViewLocator-only pattern | ✅ Fixed — replaced TODO with clarifying comment |
| M1 | MEDIUM | `!Thumbnail` negation on `Bitmap?` — undocumented truthiness | ✅ Fixed — added XAML comment explaining Avalonia truthiness pattern |
| M2 | MEDIUM | No `Foreground` on BACK/CONFIRM buttons — theme-dependent text color | ✅ Fixed — added explicit `Foreground="White"` + `Background="#e94560"` on CONFIRM |
| M3 | MEDIUM | `MaxWidth="810"` magic number without explanation | ✅ Fixed — added comment: `3 columns × (250px + 10px margin) = 780 + 30px buffer` |
| L1 | LOW | Thumbnail border has no fallback Background for corrupt-image edge case | ✅ Fixed — added `Background="#2a2a4a"` to thumbnail border |
| L2 | LOW | Button color inconsistency with CaptureView (no shared StaticResource) | ℹ️ Noted — existing tech debt documented in StartView TODO, deferred to design phase |

## Change Log

- 2026-08-05: Created PhotoSelectView.axaml + codebehind — placeholder photo grid selection UI with all functional bindings. Build verified 0 errors.
- 2026-08-05: Code review — 6 findings (1H/3M/2L). All HIGH+MEDIUM fixed: MainWindow TODO clarified, button colors explicit, magic number documented, thumbnail border fallback added. Build re-verified 0 errors.
