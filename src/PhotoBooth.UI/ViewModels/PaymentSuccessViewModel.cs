using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 6: Payment Success Notification
/// Shows success background for 3 seconds then auto-navigates to capture.
/// </summary>
public partial class PaymentSuccessViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    private readonly CancellationTokenSource _cts = new();

    public PaymentSuccessViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadBackground();
        // Start auto-navigation timer
        Dispatcher.UIThread.InvokeAsync(() => AutoNavigateToCapture(_cts.Token));
    }

    private void LoadBackground()
    {
        try
        {
            var uri = new Uri("avares://PhotoBooth.UI/Assets/backgrounds/back6 thanh cong.png");
            var oldBg = BackgroundImage;
            using var stream = AssetLoader.Open(uri);
            BackgroundImage = new Bitmap(stream);
            oldBg?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load success background: {ex.Message}");
        }
    }

    private async Task AutoNavigateToCapture(CancellationToken ct)
    {
        try
        {
            // Wait for 3 seconds
            await Task.Delay(3000, ct);
            
            if (ct.IsCancellationRequested) return;
            
            // Navigate to capture screen
            NavigationService.NavigateTo<CaptureViewModel>();
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed before timer fires
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[ERROR] Auto navigation failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        BackgroundImage?.Dispose();
        BackgroundImage = null;
    }
}

