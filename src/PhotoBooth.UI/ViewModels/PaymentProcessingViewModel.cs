using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;
using System;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 5: Payment Processing (QR Code / Waiting)
/// For now, just shows background and allows navigation to next step.
/// Actual payment logic will be added later.
/// </summary>
public partial class PaymentProcessingViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    public PaymentProcessingViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadBackground();
    }

    private void LoadBackground()
    {
        try
        {
            var uri = new Uri("avares://PhotoBooth.UI/Assets/backgrounds/back5_quetQR.png");
            BackgroundImage = new Bitmap(AssetLoader.Open(uri));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load QR background: {ex.Message}");
        }
    }

    [RelayCommand]
    private void GoNext()
    {
        // Navigate to payment success screen
        SessionService.SetPaymentComplete(true);
        NavigationService.NavigateTo<PaymentSuccessViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<PaymentAmountViewModel>();
    }
}
