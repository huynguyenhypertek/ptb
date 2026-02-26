using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Models;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 9: Add stickers to the final image
/// </summary>
public partial class StickerViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Sticker> _availableStickers = new();

    [ObservableProperty]
    private ObservableCollection<StickerPlacement> _placedStickers = new();

    [ObservableProperty]
    private Sticker? _selectedSticker;

    public StickerViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadStickers();
    }

    private void LoadStickers()
    {
        // TODO: Load from assets folder
        AvailableStickers = new ObservableCollection<Sticker>
        {
            new Sticker { Id = "1", Name = "Heart", Category = "Love" },
            new Sticker { Id = "2", Name = "Star", Category = "Fun" },
            new Sticker { Id = "3", Name = "Smile", Category = "Emoji" }
        };
    }

    [RelayCommand]
    private void AddSticker(Sticker sticker)
    {
        var placement = new StickerPlacement
        {
            Sticker = sticker,
            X = 100,
            Y = 100,
            Scale = 1.0
        };
        PlacedStickers.Add(placement);
        SessionService.AddSticker(placement);
    }

    [RelayCommand]
    private void RemoveSticker(StickerPlacement placement)
    {
        PlacedStickers.Remove(placement);
    }

    [RelayCommand]
    private void Confirm()
    {
        NavigationService.NavigateTo<ConfirmPrintViewModel>();
    }

    [RelayCommand]
    private void Skip()
    {
        NavigationService.NavigateTo<ConfirmPrintViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<PhotoSelectionViewModel>();
    }
}
