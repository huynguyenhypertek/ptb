using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Models;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 3: Frame/Background Selection
/// WARNING: NOT IN ACTIVE FLOW — This ViewModel is registered in MainWindowViewModel
/// but never navigated to. BackgroundSelectionViewModel.GoNext() navigates directly
/// to PaymentAmountViewModel, bypassing this screen entirely.
/// Kept for potential future use; remove registration if permanently unused.
/// </summary>
public partial class FrameSelectionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _selectedFrameId;

    public FrameSelectionViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
    }

    [RelayCommand]
    private void SelectFrame1()
    {
        SelectedFrameId = "frame1";
        // TODO: Map to actual Frame object when assets are available
        SessionService.SetFrame(new Frame { Id = "frame1", Name = "Frame Option 1" });
    }

    [RelayCommand]
    private void SelectFrame2()
    {
        SelectedFrameId = "frame2";
        SessionService.SetFrame(new Frame { Id = "frame2", Name = "Frame Option 2" });
    }

    [RelayCommand]
    private void GoNext()
    {
        if (!string.IsNullOrEmpty(SelectedFrameId))
        {
            NavigationService.NavigateTo<PaymentAmountViewModel>();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<BackgroundSelectionViewModel>();
    }
}
