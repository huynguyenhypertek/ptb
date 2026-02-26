using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Base class for all ViewModels with navigation support.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly NavigationService NavigationService;
    protected readonly SessionService SessionService;

    protected ViewModelBase(NavigationService navigationService, SessionService sessionService)
    {
        NavigationService = navigationService;
        SessionService = sessionService;
    }
}
