using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PhotoBooth.Admin.Services;
using PhotoBooth.Admin.ViewModels;
using PhotoBooth.Admin.Views;

namespace PhotoBooth.Admin;

public partial class App : Application
{
    private readonly ApiProcessManager _apiManager = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(_apiManager)
            };

            // Start API in background — non-blocking so UI opens immediately
            _ = _apiManager.StartAsync();

            // Kill API when Admin closes
            desktop.ShutdownRequested += (_, _) => _apiManager.Stop();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
