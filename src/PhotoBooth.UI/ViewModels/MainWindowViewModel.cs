using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Main window ViewModel that hosts the current view.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public NavigationService NavigationService { get; }
    public SessionService SessionService { get; }

    [ObservableProperty]
    private ObservableObject? _currentView;

    public MainWindowViewModel()
    {
        NavigationService = new NavigationService();
        SessionService = new SessionService();

        // Register ViewModels
        NavigationService.RegisterViewModel(() => new StartViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new LayoutSelectionViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new BackgroundSelectionViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new FrameSelectionViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new PaymentAmountViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new PaymentProcessingViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new PaymentSuccessViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new CaptureViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new PhotoSelectionViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new StickerViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new ConfirmPrintViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new PrintingViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new ThankYouViewModel(NavigationService, SessionService));
        NavigationService.RegisterViewModel(() => new QRCodeViewModel(NavigationService, SessionService));

        // Subscribe to navigation changes
        NavigationService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NavigationService.CurrentView))
            {
                CurrentView = NavigationService.CurrentView;
            }
        };

        // Start with the welcome screen
        NavigationService.NavigateTo<StartViewModel>();
    }
}
