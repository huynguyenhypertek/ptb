using System;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 1: Start/Welcome Screen — checks device status on every return
/// </summary>
public partial class StartViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isDeviceLocked;

    [ObservableProperty]
    private string _lockMessage = "";

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

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "1");
            client.Timeout = TimeSpan.FromSeconds(5);

            var apiBase = DeviceConfig.ApiBaseUrl;
            var response = await client.GetFromJsonAsync<DeviceStatusResponse>(
                $"{apiBase}/api/users/check/{deviceId}");

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
}

public class DeviceStatusResponse
{
    public bool IsEnabled { get; set; }
}
