using System;
using System.Net.Http.Json;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 1: Start/Welcome Screen — checks device status on every return
/// </summary>
public partial class StartViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private bool _isDeviceLocked;

    [ObservableProperty]
    private string _lockMessage = "";

    private bool _disposed;
    private readonly CancellationTokenSource _cts = new();

    public StartViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        // Check device status every time this screen loads
        _ = CheckDeviceStatusAsync();
    }

    private async System.Threading.Tasks.Task CheckDeviceStatusAsync()
    {
        try
        {
            var deviceId = DeviceConfig.DeviceId;
            if (string.IsNullOrEmpty(deviceId) || deviceId == "device-1") return; // skip if not logged in

            var client = HttpService.Client;

            var apiBase = DeviceConfig.ApiBaseUrl;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            var response = await client.GetFromJsonAsync<DeviceStatusResponse>(
                $"{apiBase}/api/users/check/{deviceId}", timeoutCts.Token);

            if (_disposed) return;

            if (response != null && !response.IsEnabled)
            {
                IsDeviceLocked = true;
                LockMessage = "🔒 Thiết bị đã bị khóa\nLiên hệ Admin để kích hoạt lại";
                Console.WriteLine($"[DEVICE] LOCKED - {deviceId}");
            }
            else
            {
                IsDeviceLocked = false;
                Console.WriteLine($"[DEVICE] OK - {deviceId}");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed during network call
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEVICE] Status check failed: {ex.Message}");
            // Don't lock on network error — allow offline usage
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RetryCheck()
    {
        LockMessage = "⏳ Đang kiểm tra...";
        await CheckDeviceStatusAsync();
    }

    [RelayCommand]
    private void Start()
    {
        if (IsDeviceLocked) return; // Block if locked
        SessionService.StartNewSession();
        NavigationService.NavigateTo<LayoutSelectionViewModel>();
    }

    public void Dispose()
    {
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}

public class DeviceStatusResponse
{
    public bool IsEnabled { get; set; }
}
