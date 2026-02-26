using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.UI.Services;
using System;
using System.Threading.Tasks;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 6: Payment Success Notification
/// Shows success background for 3 seconds then auto-navigates to capture.
/// </summary>
public partial class PaymentSuccessViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    public PaymentSuccessViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadBackground();
        // Start auto-navigation timer
        Dispatcher.UIThread.InvokeAsync(AutoNavigateToCapture);
    }

    private void LoadBackground()
    {
        try
        {
            var uri = new Uri("avares://PhotoBooth.UI/Assets/backgrounds/back6 thanh cong.png");
            BackgroundImage = new Bitmap(AssetLoader.Open(uri));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load success background: {ex.Message}");
        }
    }

    private async Task AutoNavigateToCapture()
    {
        try
        {
            // Wait for 3 seconds
            await Task.Delay(3000);
            
            // Navigate to capture screen
            NavigationService.NavigateTo<CaptureViewModel>();
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[ERROR] Auto navigation failed: {ex.Message}");
        }
    }
}
