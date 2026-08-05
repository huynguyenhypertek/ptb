using System;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Screen 1: Start/Welcome Screen — tap to begin a new photo session.
/// Simplified from PhotoBooth.UI version — no device lock check, no payment.
/// </summary>
public partial class StartViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private readonly SessionService _sessionService;

    public StartViewModel(NavigationService navigationService, SessionService sessionService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    /// <summary>
    /// Starts a new photo session and navigates to the capture screen.
    /// Called via the auto-generated <c>StartCommand</c> binding.
    /// </summary>
    [RelayCommand]
    private void Start()
    {
        _sessionService.StartNewSession();
        _navigationService.NavigateTo<CaptureViewModel>();
    }
}
