using PhotoBooth.Event.Services;
using PhotoBooth.Event.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoBooth.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NavigationService"/> in the PhotoBooth.Event project.
/// Covers factory registration, navigation, disposal, and edge cases.
/// </summary>
public class NavigationServiceTests
{
    // ── Test helpers ──────────────────────────────────────────

    private sealed class FakeViewModel : ViewModelBase { }

    private sealed class AnotherFakeViewModel : ViewModelBase { }

    private sealed class DisposableViewModel : ViewModelBase, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class ThrowingDisposableViewModel : ViewModelBase, IDisposable
    {
        public bool DisposeCalled { get; private set; }
        public void Dispose()
        {
            DisposeCalled = true;
            throw new InvalidOperationException("Dispose failed on purpose");
        }
    }

    // ── RegisterViewModel + NavigateTo<T> ─────────────────────

    [Fact]
    public void NavigateTo_Generic_CreatesAndAssigns_RegisteredViewModel()
    {
        var sut = new NavigationService();
        sut.RegisterViewModel(() => new FakeViewModel());

        sut.NavigateTo<FakeViewModel>();

        Assert.NotNull(sut.CurrentView);
        Assert.IsType<FakeViewModel>(sut.CurrentView);
    }

    [Fact]
    public void NavigateTo_Generic_CreatesNewInstance_EachTime()
    {
        var sut = new NavigationService();
        sut.RegisterViewModel(() => new FakeViewModel());

        sut.NavigateTo<FakeViewModel>();
        var first = sut.CurrentView;

        sut.NavigateTo<FakeViewModel>();
        var second = sut.CurrentView;

        Assert.NotSame(first, second);
    }

    [Fact]
    public void NavigateTo_Generic_UnregisteredType_DoesNotChangeCurrentView()
    {
        var sut = new NavigationService();
        var existing = new FakeViewModel();
        sut.RegisterViewModel(() => existing);
        sut.NavigateTo<FakeViewModel>();

        // AnotherFakeViewModel is NOT registered
        sut.NavigateTo<AnotherFakeViewModel>();

        // CurrentView should remain the FakeViewModel, not become null
        Assert.IsType<FakeViewModel>(sut.CurrentView);
    }

    // ── NavigateTo(ViewModelBase) overload ─────────────────────

    [Fact]
    public void NavigateTo_Instance_AssignsDirectly()
    {
        var sut = new NavigationService();
        var vm = new FakeViewModel();

        sut.NavigateTo(vm);

        Assert.Same(vm, sut.CurrentView);
    }

    // ── Disposal behavior ─────────────────────────────────────

    [Fact]
    public void NavigateTo_Generic_DisposesOldView_WhenDisposable()
    {
        var sut = new NavigationService();
        var disposable = new DisposableViewModel();
        sut.NavigateTo(disposable);

        sut.RegisterViewModel(() => new FakeViewModel());
        sut.NavigateTo<FakeViewModel>();

        Assert.True(disposable.IsDisposed);
    }

    [Fact]
    public void NavigateTo_Instance_DisposesOldView_WhenDisposable()
    {
        var sut = new NavigationService();
        var disposable = new DisposableViewModel();
        sut.NavigateTo(disposable);

        sut.NavigateTo(new FakeViewModel());

        Assert.True(disposable.IsDisposed);
    }

    [Fact]
    public void NavigateTo_DoesNotThrow_WhenOldViewDisposeFails()
    {
        var sut = new NavigationService();
        var throwing = new ThrowingDisposableViewModel();
        sut.NavigateTo(throwing);

        // Should NOT throw — dispose errors are caught and logged
        var exception = Record.Exception(() => sut.NavigateTo(new FakeViewModel()));

        Assert.Null(exception);
        Assert.True(throwing.DisposeCalled);
    }

    [Fact]
    public void NavigateTo_NoDispose_WhenOldViewNotDisposable()
    {
        var sut = new NavigationService();
        var nonDisposable = new FakeViewModel();
        sut.NavigateTo(nonDisposable);

        // Just navigating away — no dispose, no crash
        var exception = Record.Exception(() => sut.NavigateTo(new AnotherFakeViewModel()));

        Assert.Null(exception);
    }

    // ── PropertyChanged notification ──────────────────────────

    [Fact]
    public void NavigateTo_Generic_RaisesPropertyChanged_ForCurrentView()
    {
        var sut = new NavigationService();
        sut.RegisterViewModel(() => new FakeViewModel());

        var propertyNames = new List<string>();
        sut.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

        sut.NavigateTo<FakeViewModel>();

        Assert.Contains("CurrentView", propertyNames);
    }

    [Fact]
    public void NavigateTo_Instance_RaisesPropertyChanged_ForCurrentView()
    {
        var sut = new NavigationService();

        var propertyNames = new List<string>();
        sut.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

        sut.NavigateTo(new FakeViewModel());

        Assert.Contains("CurrentView", propertyNames);
    }

    // ── Initial state ─────────────────────────────────────────

    [Fact]
    public void CurrentView_IsNull_Initially()
    {
        var sut = new NavigationService();

        Assert.Null(sut.CurrentView);
    }

    // ── Same-type re-navigation disposal ─────────────────────

    [Fact]
    public void NavigateTo_Generic_SameType_DisposesOldInstance()
    {
        var sut = new NavigationService();
        var first = new DisposableViewModel();
        sut.RegisterViewModel(() => new DisposableViewModel());

        // Seed with a known disposable instance
        sut.NavigateTo(first);

        // Navigate to same type — old instance should be disposed
        sut.NavigateTo<DisposableViewModel>();

        Assert.True(first.IsDisposed);
        Assert.NotSame(first, sut.CurrentView);
    }

    // ── Registration overwrite ────────────────────────────────

    [Fact]
    public void RegisterViewModel_OverwritesPreviousFactory_ForSameType()
    {
        var sut = new NavigationService();
        var firstVm = new FakeViewModel();
        var secondVm = new FakeViewModel();

        sut.RegisterViewModel(() => firstVm);
        sut.RegisterViewModel(() => secondVm);
        sut.NavigateTo<FakeViewModel>();

        Assert.Same(secondVm, sut.CurrentView);
    }
}
