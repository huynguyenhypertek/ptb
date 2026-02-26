using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;
using System;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 4: Payment Amount Display
/// Shows dynamic background based on selected layout (70k for layout6, 50k for layout2)
/// </summary>
public partial class PaymentAmountViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    public PaymentAmountViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadPaymentBackground();
    }

    private void LoadPaymentBackground()
    {
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        Console.WriteLine($"[DEBUG] Loading Payment Background for Layout: {layoutId}");

        string imagePath;
        if (layoutId == "layout6")
        {
            imagePath = "avares://PhotoBooth.UI/Assets/backgrounds/back4_70k.png";
        }
        else
        {
            imagePath = "avares://PhotoBooth.UI/Assets/backgrounds/back4_50k.png";
        }

        try
        {
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(imagePath)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load payment background: {ex.Message}");
        }
    }

    [RelayCommand]
    private void GoNext()
    {
        NavigationService.NavigateTo<PaymentProcessingViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<BackgroundSelectionViewModel>();
    }
}
