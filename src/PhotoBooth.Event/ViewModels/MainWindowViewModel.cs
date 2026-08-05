using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Main window ViewModel that hosts the current view.
/// Creates NavigationService, SessionService, and shared CameraService.
/// Registers factories for the 4 Event screens and starts on StartViewModel.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    public NavigationService NavigationService { get; }
    public SessionService SessionService { get; }

    /// <summary>
    /// Singleton camera service shared across all sessions.
    /// Created once at startup, disposed when app exits.
    /// Prevents native VideoCapture handle leaks from per-session creation.
    /// </summary>
    private readonly ICameraService _sharedCameraService = new CameraService();

    private bool _disposed;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel()
    {
        NavigationService = new NavigationService();
        SessionService = new SessionService();

        // Register the 4 Event screen ViewModels
        NavigationService.RegisterViewModel(() => new StartViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new CaptureViewModel(NavigationService, SessionService, _sharedCameraService));
        NavigationService.RegisterViewModel(() => new PhotoSelectViewModel(NavigationService, SessionService));
        // Print service is needed for ReviewPrintViewModel
        var printService = new PrintService();
        NavigationService.RegisterViewModel(() => new ReviewPrintViewModel(NavigationService, SessionService, printService));

        // Subscribe to navigation changes — relay CurrentView to MainWindow's ContentControl binding
        NavigationService.PropertyChanged += OnNavigationServicePropertyChanged;

        // Start with the welcome screen
        NavigationService.NavigateTo<StartViewModel>();
    }

    private void OnNavigationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationService.CurrentView))
        {
            CurrentView = NavigationService.CurrentView;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        NavigationService.PropertyChanged -= OnNavigationServicePropertyChanged;
        _sharedCameraService.Dispose();
        GC.SuppressFinalize(this);
    }
}
