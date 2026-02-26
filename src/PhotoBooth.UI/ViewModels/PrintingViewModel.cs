using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 11: Printing progress
/// </summary>
public partial class PrintingViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _progress = 0;

    [ObservableProperty]
    private string _statusMessage = "Đang chuẩn bị in...";

    public PrintingViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        _ = StartPrintingAsync();
    }

    private async Task StartPrintingAsync()
    {
        // Simulate printing progress
        StatusMessage = "Đang xử lý ảnh...";
        for (int i = 0; i <= 30; i++)
        {
            Progress = i;
            await Task.Delay(50);
        }

        StatusMessage = "Đang gửi đến máy in...";
        for (int i = 30; i <= 60; i++)
        {
            Progress = i;
            await Task.Delay(50);
        }

        // TODO: Actual print job
        StatusMessage = "Đang in...";
        for (int i = 60; i <= 100; i++)
        {
            Progress = i;
            await Task.Delay(50);
        }

        await Task.Delay(500);
        NavigationService.NavigateTo<QRCodeViewModel>();
    }
}
