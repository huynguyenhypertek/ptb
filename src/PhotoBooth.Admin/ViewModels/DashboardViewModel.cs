using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private int _totalSessions;

    [ObservableProperty]
    private int _todaySessions;

    [ObservableProperty]
    private int _totalPhotos;

    [ObservableProperty]
    private string _totalRevenue = "0";

    [ObservableProperty]
    private string _todayRevenue = "0";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _lastUpdated = "";

    [ObservableProperty]
    private DeviceLauncherViewModel _deviceLauncher;

    public DashboardViewModel(ApiService apiService, DeviceLauncherViewModel deviceLauncher)
    {
        _apiService = apiService;
        DeviceLauncher = deviceLauncher;
        LoadStatsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadStats()
    {
        IsLoading = true;
        try
        {
            var stats = await _apiService.GetStatsAsync();
            if (stats != null)
            {
                TotalSessions = stats.TotalSessions;
                TodaySessions = stats.TodaySessions;
                TotalPhotos = stats.TotalPhotos;
                TotalRevenue = stats.TotalRevenue.ToString("N0") + "đ";
                TodayRevenue = stats.TodayRevenue.ToString("N0") + "đ";
            }
            LastUpdated = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stats: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
