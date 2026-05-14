using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    private readonly CancellationTokenSource _cts = new();

    public PrintingViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        _ = StartPrintingAsync(_cts.Token);
    }

    private async Task StartPrintingAsync(CancellationToken ct)
    {
        try
        {
            // Finding 5: Gate on SimulatePrint so setting it to false has effect
            if (!SimulatePrint)
            {
                // TODO: Real printer SDK integration goes here
                StatusMessage = "⚠️ Real printing not yet implemented";
                Console.WriteLine("[PRINT] SimulatePrint=false but no real printer SDK — skipping");
                await Task.Delay(2000, ct);
                if (!ct.IsCancellationRequested)
                    NavigationService.NavigateTo<QRCodeViewModel>();
                return;
            }

            // WARNING: SIMULATED — all progress steps are artificial delays
            StatusMessage = "Đang xử lý ảnh...";
            for (int i = 0; i <= 30; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = i;
                await Task.Delay(50, ct);
            }

            StatusMessage = "Đang gửi đến máy in...";
            for (int i = 30; i <= 60; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = i;
                await Task.Delay(50, ct);
            }

            StatusMessage = "Đang in...";
            for (int i = 60; i <= 100; i++)
            {
                ct.ThrowIfCancellationRequested();
                Progress = i;
                await Task.Delay(50, ct);
            }

            await Task.Delay(500, ct);
            
            if (!ct.IsCancellationRequested)
            {
                NavigationService.NavigateTo<QRCodeViewModel>();
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
