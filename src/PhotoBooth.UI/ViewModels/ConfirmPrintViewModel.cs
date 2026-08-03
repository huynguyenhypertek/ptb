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
public partial class ConfirmPrintViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    [ObservableProperty]
    private Bitmap? _finalImage;

    [ObservableProperty]
    private int _printCopies = 1;

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
            var oldBg = BackgroundImage;
            using var stream = AssetLoader.Open(new Uri(bgPath));
            BackgroundImage = new Bitmap(stream);
            oldBg?.Dispose();
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
            using var fileStream = File.OpenRead(path);
            using var memStream = new MemoryStream();
            fileStream.CopyTo(memStream);
            memStream.Position = 0;
            var oldFinal = FinalImage;
            FinalImage = new Bitmap(memStream);
            oldFinal?.Dispose();
            Console.WriteLine($"[DISPLAY] Loaded final image: {path}");
        }
        else
        {
            Console.WriteLine($"[ERROR] Final image not found: {path}");
        }
    }

    [RelayCommand]
    private void IncreaseCopies()
    {
        if (PrintCopies < 10) PrintCopies++;
    }

    [RelayCommand]
    private void DecreaseCopies()
    {
        if (PrintCopies > 1) PrintCopies--;
    }

    [RelayCommand]
    private void GoNext()
    {
        SessionService.CurrentSession.PrintCopies = PrintCopies;
        NavigationService.NavigateTo<PrintingViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<PhotoSelectionViewModel>();
    }

    public void Dispose()
    {
        BackgroundImage?.Dispose();
        BackgroundImage = null;
        FinalImage?.Dispose();
        FinalImage = null;
    }
}
