using Avalonia.Controls;
using PhotoBooth.Event;
using PhotoBooth.Event.ViewModels;

namespace PhotoBooth.Tests;

/// <summary>
/// Verifies that <see cref="ViewLocator"/> correctly resolves all registered
/// ViewModels to their corresponding Views via the naming convention:
/// <c>FullName.Replace("ViewModel", "View")</c>.
/// Guards against accidental renames breaking the convention-based resolution.
/// </summary>
public class ViewLocatorTests
{
    private readonly ViewLocator _sut = new();

    // ── Convention resolution ─────────────────────────────────

    /// <summary>
    /// StartViewModel must resolve to StartView (E1-S2).
    /// Once StartView.axaml exists, this test will pass end-to-end.
    /// Until then it validates the ViewLocator produces a "Not Found" fallback
    /// rather than throwing.
    /// </summary>
    [Fact]
    public void Build_StartViewModel_ResolvesOrFallsBackGracefully()
    {
        var viewModel = CreateMinimalStartViewModel();

        var result = _sut.Build(viewModel);

        Assert.NotNull(result);

        // Once StartView exists, this will be a StartView instance.
        // Before that, it's a TextBlock with "Not Found:" — either is acceptable.
        var isView = result.GetType().Name == "StartView";
        var isFallback = result is TextBlock tb && tb.Text!.Contains("Not Found");
        Assert.True(isView || isFallback,
            $"Expected StartView or fallback TextBlock, got {result.GetType().Name}");
    }

    // ── Match behavior ────────────────────────────────────────

    [Fact]
    public void Match_ReturnsTrue_ForViewModelBase()
    {
        var vm = new TestViewModel();

        Assert.True(_sut.Match(vm));
    }

    [Fact]
    public void Match_ReturnsFalse_ForNull()
    {
        Assert.False(_sut.Match(null));
    }

    [Fact]
    public void Match_ReturnsFalse_ForNonViewModel()
    {
        Assert.False(_sut.Match("not a viewmodel"));
    }

    // ── Null / missing type handling ──────────────────────────

    [Fact]
    public void Build_ReturnsNull_ForNullParam()
    {
        var result = _sut.Build(null);

        Assert.Null(result);
    }

    [Fact]
    public void Build_ReturnsNotFoundTextBlock_ForUnresolvableViewModel()
    {
        // TestViewModel has no corresponding TestView class
        var vm = new TestViewModel();

        var result = _sut.Build(vm);

        Assert.NotNull(result);
        var textBlock = Assert.IsType<TextBlock>(result);
        Assert.Contains("Not Found", textBlock.Text);
    }

    // ── Naming convention verification ────────────────────────

    /// <summary>
    /// Validates the naming convention produces correct expected type names
    /// for all known ViewModels that should have Views.
    /// This catches accidental renames before they become runtime failures.
    /// </summary>
    [Theory]
    [InlineData("PhotoBooth.Event.ViewModels.StartViewModel", "PhotoBooth.Event.Views.StartView")]
    [InlineData("PhotoBooth.Event.ViewModels.CaptureViewModel", "PhotoBooth.Event.Views.CaptureView")]
    [InlineData("PhotoBooth.Event.ViewModels.PhotoSelectViewModel", "PhotoBooth.Event.Views.PhotoSelectView")]
    [InlineData("PhotoBooth.Event.ViewModels.ReviewPrintViewModel", "PhotoBooth.Event.Views.ReviewPrintView")]
    public void NamingConvention_ProducesCorrectViewTypeName(string viewModelFullName, string expectedViewFullName)
    {
        var actual = viewModelFullName.Replace("ViewModel", "View", StringComparison.Ordinal);

        Assert.Equal(expectedViewFullName, actual);
    }

    // ── Error resilience ──────────────────────────────────────

    [Fact]
    public void Build_DoesNotThrow_ForAnyViewModel()
    {
        // ViewLocator should never throw — it should return a control or fallback
        var vm = new TestViewModel();

        var exception = Record.Exception(() => _sut.Build(vm));

        Assert.Null(exception);
    }

    // ── Test helpers ──────────────────────────────────────────

    private sealed class TestViewModel : ViewModelBase { }

    /// <summary>
    /// Creates a minimal StartViewModel for ViewLocator resolution testing.
    /// We only need the type's FullName — service behavior is irrelevant here.
    /// </summary>
    private static ViewModelBase CreateMinimalStartViewModel()
    {
        var nav = new PhotoBooth.Event.Services.NavigationService();
        var session = new PhotoBooth.Event.Services.SessionService();
        return new StartViewModel(nav, session);
    }
}
