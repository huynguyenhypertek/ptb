using System.IO;
using System.Threading.Tasks;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Event.Services;
using PhotoBooth.Event.ViewModels;

namespace PhotoBooth.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="PhotoSelectViewModel"/> in the PhotoBooth.Event project.
/// Covers selection logic, toggle, ordering, confirm/goback navigation, dispose, and edge cases.
/// Note: Bitmap-dependent tests (LoadThumbnail, SelectedPhoto slots) are skipped because
/// Avalonia Bitmap requires a platform runtime. Tests focus on logic paths that don't
/// touch Bitmap APIs.
/// </summary>
public class PhotoSelectViewModelTests
{
    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Minimal stub for ICameraService — CaptureViewModel requires it in its constructor.
    /// All methods are no-ops; only used to satisfy DI without a real camera.
    /// </summary>
#pragma warning disable CS0067
    private sealed class StubCameraService : ICameraService
    {
        public event EventHandler<byte[]>? FrameReady;
        public event EventHandler? CameraError;
        public bool IsRunning => false;
        public bool IsInitialized => false;
        public bool Initialize(int deviceIndex = 0) => false;
        public void Deinitialize() { }
        public void StartPreview() { }
        public void StopPreview() { }
        public string CapturePhoto(string outputDirectory, string? fileName = null) => string.Empty;
        public byte[]? GetCurrentFrame() => null;
        public void Dispose() { }
    }
#pragma warning restore CS0067

    private sealed class StubPrintService : IPrintService
    {
        public Task<bool> PrintImageAsync(string imagePath, string printerName, int copies, CancellationToken ct, string mediaType = "") => Task.FromResult(true);
    }

    private static NavigationService CreateNavService()
    {
        var nav = new NavigationService();
        var session = new SessionService();
        var camera = new StubCameraService();
        var print = new StubPrintService();
        // Register real ViewModel types so NavigateTo<T>() type lookup matches
        nav.RegisterViewModel(() => new ReviewPrintViewModel(nav, session, print));
        nav.RegisterViewModel(() => new CaptureViewModel(nav, session, camera));
        return nav;
    }

    private static SessionService CreateSessionWithPhotos(int count = 6)
    {
        var session = new SessionService();
        session.StartNewSession();
        // Add fake photo paths (files won't exist — LoadThumbnail returns null gracefully)
        for (int i = 0; i < count; i++)
        {
            session.AddCapturedPhoto(Path.Combine(Path.GetTempPath(), $"fake_photo_{i}.jpg"));
        }
        return session;
    }

    // ── Constructor & Loading (Task 1, 2, 3) ─────────────────────

    [Fact]
    public void Constructor_LoadsPhotosFromSession()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos(6);

        var vm = new PhotoSelectViewModel(nav, session);

        Assert.Equal(6, vm.Photos.Count);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i, vm.Photos[i].Index);
            Assert.Equal(Path.Combine(Path.GetTempPath(), $"fake_photo_{i}.jpg"), vm.Photos[i].Path);
            Assert.Null(vm.Photos[i].Thumbnail); // Files don't exist → null
            Assert.False(vm.Photos[i].IsSelected);
        }
    }

    [Fact]
    public void Constructor_EmptySession_LoadsZeroPhotos()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos(0);

        var vm = new PhotoSelectViewModel(nav, session);

        Assert.Empty(vm.Photos);
    }

    [Fact]
    public void Constructor_ThrowsOnNullNavigationService()
    {
        var session = CreateSessionWithPhotos();
        Assert.Throws<ArgumentNullException>(() => new PhotoSelectViewModel(null!, session));
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionService()
    {
        var nav = CreateNavService();
        Assert.Throws<ArgumentNullException>(() => new PhotoSelectViewModel(nav, null!));
    }

    // ── SelectedCount & CanConfirm (Task 2) ─────────────────────

    [Fact]
    public void SelectedCount_IsZero_Initially()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void CanConfirm_IsFalse_Initially()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());
        Assert.False(vm.CanConfirm);
    }

    // ── ToggleSelection (Task 4) ─────────────────────────────────

    [Fact]
    public void ToggleSelection_SelectsPhoto_WhenNotSelected()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        vm.ToggleSelectionCommand.Execute(vm.Photos[0]);

        Assert.True(vm.Photos[0].IsSelected);
        Assert.Equal(1, vm.SelectedCount);
    }

    [Fact]
    public void ToggleSelection_DeselectsPhoto_WhenAlreadySelected()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        vm.ToggleSelectionCommand.Execute(vm.Photos[0]);
        vm.ToggleSelectionCommand.Execute(vm.Photos[0]); // Deselect

        Assert.False(vm.Photos[0].IsSelected);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void ToggleSelection_CanSelect_UpTo4Photos()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        Assert.Equal(4, vm.SelectedCount);
        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void ToggleSelection_RejectsSelection_WhenAtMax()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        // Select 4
        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        // Try to select 5th — should be rejected
        vm.ToggleSelectionCommand.Execute(vm.Photos[4]);

        Assert.False(vm.Photos[4].IsSelected);
        Assert.Equal(4, vm.SelectedCount);
    }

    [Fact]
    public void ToggleSelection_CanDeselectAndReselectDifferent()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        // Select 4 photos (0,1,2,3)
        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        // Deselect photo 0
        vm.ToggleSelectionCommand.Execute(vm.Photos[0]);
        Assert.Equal(3, vm.SelectedCount);
        Assert.False(vm.CanConfirm);

        // Select photo 4 instead
        vm.ToggleSelectionCommand.Execute(vm.Photos[4]);
        Assert.Equal(4, vm.SelectedCount);
        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void ToggleSelection_NullPhoto_DoesNotThrow()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        // M1 fix: null guard should prevent NullReferenceException
        var exception = Record.Exception(() => vm.ToggleSelectionCommand.Execute(null));

        Assert.Null(exception);
        Assert.Equal(0, vm.SelectedCount); // No side effects
    }

    [Fact]
    public void ToggleSelection_RaisesPropertyChanged_ForSelectedCountAndCanConfirm()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.ToggleSelectionCommand.Execute(vm.Photos[0]);

        Assert.Contains("SelectedCount", changedProperties);
        Assert.Contains("CanConfirm", changedProperties);
    }

    // ── Selection Order (Task 4, 5) ──────────────────────────────

    [Fact]
    public async Task Confirm_SavesIndices_InSelectionOrder()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos();
        var vm = new PhotoSelectViewModel(nav, session);

        // Select photos in order: 3, 1, 5, 0
        vm.ToggleSelectionCommand.Execute(vm.Photos[3]);
        vm.ToggleSelectionCommand.Execute(vm.Photos[1]);
        vm.ToggleSelectionCommand.Execute(vm.Photos[5]);
        vm.ToggleSelectionCommand.Execute(vm.Photos[0]);

        await vm.ConfirmCommand.ExecuteAsync(null);

        // Verify session got indices in tap order
        Assert.Equal(new List<int> { 3, 1, 5, 0 }, session.CurrentSession.SelectedPhotoIndices);
    }

    // ── Confirm Command (Task 6) ─────────────────────────────────

    [Fact]
    public void ConfirmCommand_CannotExecute_WhenLessThan4Selected()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        // Select only 2
        vm.ToggleSelectionCommand.Execute(vm.Photos[0]);
        vm.ToggleSelectionCommand.Execute(vm.Photos[1]);

        Assert.False(vm.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public void ConfirmCommand_CanExecute_When4Selected()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        Assert.True(vm.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Confirm_NavigatesToReviewPrintViewModel()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos();
        var vm = new PhotoSelectViewModel(nav, session);

        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.IsType<ReviewPrintViewModel>(nav.CurrentView);
    }

    [Fact]
    public async Task Confirm_SetsSelectedPhotoIndicesOnSession()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos();
        var vm = new PhotoSelectViewModel(nav, session);

        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(4, session.CurrentSession.SelectedPhotoIndices.Count);
        Assert.Equal(new List<int> { 0, 1, 2, 3 }, session.CurrentSession.SelectedPhotoIndices);
    }

    [Fact]
    public async Task ConfirmCommand_PreventsDoubleSubmission()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos();
        var vm = new PhotoSelectViewModel(nav, session);

        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        // Execute twice concurrently to test the _isCompositing guard
        var task1 = vm.ConfirmCommand.ExecuteAsync(null);
        var task2 = vm.ConfirmCommand.ExecuteAsync(null);
        
        await Task.WhenAll(task1, task2);

        Assert.Equal(4, session.CurrentSession.SelectedPhotoIndices.Count);
    }

    // ── GoBack Command (Task 7) ──────────────────────────────────

    [Fact]
    public void GoBack_NavigatesToCaptureViewModel()
    {
        var nav = CreateNavService();
        var session = CreateSessionWithPhotos();
        var vm = new PhotoSelectViewModel(nav, session);

        vm.GoBackCommand.Execute(null);

        Assert.IsType<CaptureViewModel>(nav.CurrentView);
    }

    // ── Dispose (Task 8) ─────────────────────────────────────────

    [Fact]
    public void Dispose_NullsOutPreviewSlots()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        vm.Dispose();

        Assert.Null(vm.SelectedPhoto1);
        Assert.Null(vm.SelectedPhoto2);
        Assert.Null(vm.SelectedPhoto3);
        Assert.Null(vm.SelectedPhoto4);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        // Should not throw on double-dispose
        var exception = Record.Exception(() =>
        {
            vm.Dispose();
            vm.Dispose();
        });

        Assert.Null(exception);
    }

    // ── Edge Cases ───────────────────────────────────────────────

    [Fact]
    public void PartialCapture_FewerThan6Photos_CannotReach4Selections()
    {
        var session = CreateSessionWithPhotos(3); // Only 3 photos captured
        var vm = new PhotoSelectViewModel(CreateNavService(), session);

        Assert.Equal(3, vm.Photos.Count);

        // Select all 3
        for (int i = 0; i < 3; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        Assert.Equal(3, vm.SelectedCount);
        Assert.False(vm.CanConfirm); // Can't reach 4 — button stays disabled
    }

    [Fact]
    public void SelectExactly4_ThenDeselectAll_ConfirmDisabled()
    {
        var vm = new PhotoSelectViewModel(CreateNavService(), CreateSessionWithPhotos());

        // Select 4
        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);
        Assert.True(vm.CanConfirm);

        // Deselect all 4
        for (int i = 0; i < 4; i++)
            vm.ToggleSelectionCommand.Execute(vm.Photos[i]);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CanConfirm);
    }
}
