using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 10: Display the final composed image (photos + frame overlay)
/// The composite image was already created and saved in PhotoSelectionViewModel.
/// </summary>
public partial class ConfirmPrintViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    [ObservableProperty]
    private Bitmap? _finalImage;

    public ConfirmPrintViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadBackground();
        LoadFinalImage();
    }

    private void LoadBackground()
    {
        try
        {
            var bgPath = "avares://PhotoBooth.UI/Assets/backgrounds/nen10 hien thi anh.png";
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load background: {ex.Message}");
        }
    }

    private void LoadFinalImage()
    {
        var path = SessionService.CurrentSession.FinalImagePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            FinalImage = new Bitmap(path);
            Console.WriteLine($"[DISPLAY] Loaded final image: {path}");
        }
        else
        {
            Console.WriteLine($"[ERROR] Final image not found: {path}");
        }
    }

    [RelayCommand]
    private void GoNext()
    {
        NavigationService.NavigateTo<ThankYouViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<PhotoSelectionViewModel>();
    }
}
