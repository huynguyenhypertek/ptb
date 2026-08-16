using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 11: Printing progress
/// </summary>
public partial class PrintingViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private int _progress = 0;

    [ObservableProperty]
    private string _statusMessage = "Đang chuẩn bị in...";

    private readonly IPrintService _printService;
    private readonly CancellationTokenSource _cts = new();

    public PrintingViewModel(NavigationService navigationService, SessionService sessionService, IPrintService printService) 
        : base(navigationService, sessionService)
    {
        _printService = printService;
        _ = StartPrintingAsync(_cts.Token);
    }

    private async Task StartPrintingAsync(CancellationToken ct)
    {
        try
        {
            // Tôn trọng công tắc in của Admin — không giả lập thành công khi
            // chưa cấu hình đủ máy in, để khách không ra về tay không mà tin đã in.
            if (!DeviceConfig.EnablePrinting)
            {
                StatusMessage = "Đã lưu ảnh (chế độ không in)";
                Progress = 100;
                await Task.Delay(1500, ct);
                if (!ct.IsCancellationRequested)
                    NavigationService.NavigateTo<ThankYouViewModel>();
                return;
            }

            if (string.IsNullOrEmpty(DeviceConfig.PrinterName) || string.IsNullOrEmpty(SessionService.CurrentSession.FinalImagePath))
            {
                StatusMessage = "⚠️ Chưa cấu hình máy in — gọi nhân viên hỗ trợ";
                await Task.Delay(3000, ct);
                if (!ct.IsCancellationRequested)
                    NavigationService.NavigateTo<ThankYouViewModel>();
                return;
            }

            StatusMessage = "Đang gửi đến máy in...";
            Progress = 50;

            var success = await _printService.PrintImageAsync(
                SessionService.CurrentSession.FinalImagePath,
                DeviceConfig.PrinterName,
                SessionService.CurrentSession.PrintCopies,
                ct);

            if (success)
            {
                StatusMessage = "Đã gửi lệnh in thành công!";
                Progress = 100;
                await Task.Delay(1500, ct);
            }
            else
            {
                StatusMessage = "⚠️ Lỗi khi gửi lệnh in!";
                await Task.Delay(3000, ct);
            }

            if (!ct.IsCancellationRequested)
            {
                NavigationService.NavigateTo<ThankYouViewModel>();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed before printing completes
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Printing failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
