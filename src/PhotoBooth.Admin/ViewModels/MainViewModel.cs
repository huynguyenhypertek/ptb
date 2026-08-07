using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ApiService _apiService = new();
    private readonly SettingsService _settingsService = new();
    private readonly ApiProcessManager? _apiProcessManager;

    // BUG FIX: Singleton — prevent creating a new ViewModel on every tab switch,
    // which would discard all Process references and show devices as "Đã dừng".
    private DeviceLauncherViewModel? _deviceLauncherViewModel;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _currentUser = "";

    [ObservableProperty]
    private string _currentRole = "";

    [ObservableProperty]
    private string _currentStore = "";

    [ObservableProperty]
    private bool _isSystemAdmin;

    // Theme
    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _themeIcon = "🌙";

    [ObservableProperty]
    private IBrush _windowBg = new SolidColorBrush(Color.Parse("#11111b"));

    [ObservableProperty]
    private IBrush _sidebarBg = new SolidColorBrush(Color.Parse("#1e1e2e"));

    [ObservableProperty]
    private IBrush _cardBg = new SolidColorBrush(Color.Parse("#2a2a3e"));

    [ObservableProperty]
    private IBrush _textColor = Brushes.White;

    [ObservableProperty]
    private IBrush _subtextColor = new SolidColorBrush(Color.Parse("#888888"));

    public MainViewModel(ApiProcessManager? apiProcessManager = null)
    {
        _apiProcessManager = apiProcessManager;
        CurrentView = new LoginViewModel(_apiService, OnLoginSuccess);
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentView = new SettingsViewModel(_settingsService);
    }

    private void OnLoginSuccess(string username)
    {
        IsLoggedIn = true;
        CurrentUser = username;
        CurrentRole = _apiService.Role ?? "";
        CurrentStore = _apiService.StoreName ?? "Tất cả";
        IsSystemAdmin = _apiService.Role == "SystemAdmin";
        CurrentView = new DashboardViewModel(_apiService, GetOrCreateDeviceLauncher());
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;

        if (IsDarkTheme)
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            ThemeIcon = "🌙";
            WindowBg = new SolidColorBrush(Color.Parse("#11111b"));
            SidebarBg = new SolidColorBrush(Color.Parse("#1e1e2e"));
            CardBg = new SolidColorBrush(Color.Parse("#2a2a3e"));
            TextColor = Brushes.White;
            SubtextColor = new SolidColorBrush(Color.Parse("#888888"));
        }
        else
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            ThemeIcon = "☀️";
            WindowBg = new SolidColorBrush(Color.Parse("#f5f5f5"));
            SidebarBg = new SolidColorBrush(Color.Parse("#ffffff"));
            CardBg = new SolidColorBrush(Color.Parse("#e8e8f0"));
            TextColor = Brushes.Black;
            SubtextColor = new SolidColorBrush(Color.Parse("#555555"));
        }
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        CurrentView = new DashboardViewModel(_apiService, GetOrCreateDeviceLauncher());
    }

    [RelayCommand]
    private void ShowSessions()
    {
        CurrentView = new SessionListViewModel(_apiService);
    }

    [RelayCommand]
    private void ShowStores()
    {
        CurrentView = new StoreListViewModel(_apiService);
    }

    [RelayCommand]
    private void ShowUsers()
    {
        CurrentView = new UserListViewModel(_apiService);
    }

    [RelayCommand]
    private void ShowFrames()
    {
        CurrentView = new FrameListViewModel(_apiService);
    }

    [RelayCommand]
    private void ShowPlans()
    {
        CurrentView = new PlanListViewModel(_apiService);
    }

    private DeviceLauncherViewModel GetOrCreateDeviceLauncher()
    {
        _deviceLauncherViewModel ??= new DeviceLauncherViewModel(_apiService, _settingsService);
        return _deviceLauncherViewModel;
    }

    [RelayCommand]
    private void ShowDeviceLauncher()
    {
        CurrentView = GetOrCreateDeviceLauncher();
    }

    [RelayCommand]
    private void Logout()
    {
        IsLoggedIn = false;
        CurrentUser = "";
        CurrentRole = "";
        CurrentStore = "";
        IsSystemAdmin = false;
        CurrentView = new LoginViewModel(_apiService, OnLoginSuccess);
    }
}
