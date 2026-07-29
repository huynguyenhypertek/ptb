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
/// WARNING: ENTIRE PRINT FLOW IS SIMULATED — no real printer integration exists.
/// All progress steps are artificial delays. Set SimulatePrint = false when real
/// printer SDK is integrated.
/// </summary>
public partial class PrintingViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// When true, printing is simulated with artificial delays.
    /// Set to false when real printer integration is available.
    /// </summary>
    public static bool SimulatePrint { get; set; } = true;

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
            if (DeviceConfig.EnablePrinting && !string.IsNullOrEmpty(DeviceConfig.PrinterName) && !string.IsNullOrEmpty(SessionService.CurrentSession.FinalImagePath))
            {
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
                return;
            }

            // SIMULATED PRINTING FALLBACK
            StatusMessage = "Đang xử lý ảnh...";
            for (int i = 0; i <= 30; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = i;
                await Task.Delay(50, ct);
            }

            StatusMessage = "Đang gửi đến máy in (Giả lập)...";
            for (int i = 30; i <= 60; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = i;
                await Task.Delay(50, ct);
            }

            StatusMessage = "Đang in (Giả lập)...";
            for (int i = 60; i <= 100; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = i;
                await Task.Delay(50, ct);
            }

            await Task.Delay(500, ct);
            
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
