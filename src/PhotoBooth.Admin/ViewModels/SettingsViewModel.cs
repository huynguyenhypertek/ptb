using System;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private bool _googleDriveEnabled;

    [ObservableProperty]
    private string _googleDrivePath = "";

    [ObservableProperty]
    private string _appsScriptUrl = "";

    [ObservableProperty]
    private bool _enablePrinting;

    [ObservableProperty]
    private string _printerName = "";

    [ObservableProperty]
    private int _countdownSeconds = 3;

    [ObservableProperty]
    private string _eventName = "DONGFEST";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _folderStatus = "";

    [ObservableProperty]
    private string _testUrlResult = "";

    [ObservableProperty]
    private bool _isTesting;

    /// <summary>
    /// Event raised when the ViewModel needs the View to open a folder picker.
    /// The View subscribes to this and calls back with the selected path.
    /// </summary>
    public event Action? BrowseFolderRequested;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;

        // Load current values
        GoogleDriveEnabled = settingsService.Settings.GoogleDriveEnabled;
        GoogleDrivePath = settingsService.Settings.GoogleDrivePath;
        AppsScriptUrl = settingsService.Settings.AppsScriptUrl;
        EnablePrinting = settingsService.Settings.EnablePrinting;
        PrinterName = settingsService.Settings.PrinterName;
        CountdownSeconds = settingsService.Settings.CountdownSeconds;
        EventName = settingsService.Settings.EventName;

        UpdateFolderStatus();
    }

    partial void OnGoogleDrivePathChanged(string value)
    {
        UpdateFolderStatus();
    }

    private void UpdateFolderStatus()
    {
        if (string.IsNullOrWhiteSpace(GoogleDrivePath))
        {
            FolderStatus = "⚠️ Chưa cấu hình đường dẫn";
        }
        else if (Directory.Exists(GoogleDrivePath))
        {
            FolderStatus = "✅ Thư mục tồn tại";
        }
        else
        {
            FolderStatus = "❌ Thư mục không tìm thấy — kiểm tra Google Drive Desktop đang chạy";
        }
    }

    [RelayCommand]
    private void BrowseGoogleDrivePath()
    {
        // Raise event — the View will handle the native folder dialog
        BrowseFolderRequested?.Invoke();
    }

    /// <summary>
    /// Called by the View after folder picker returns a result
    /// </summary>
    public void SetGoogleDrivePath(string path)
    {
        GoogleDrivePath = path;
    }

    [RelayCommand]
    private async Task TestAppsScriptUrl()
    {
        if (string.IsNullOrWhiteSpace(AppsScriptUrl))
        {
            TestUrlResult = "❌ Chưa nhập URL";
            return;
        }

        IsTesting = true;
        TestUrlResult = "⏳ Đang test...";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var response = await client.GetAsync(AppsScriptUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (content.Contains("Missing") && content.Contains("folder"))
            {
                TestUrlResult = "✅ URL hoạt động! (trả về: Missing 'folder' parameter — đúng)";
            }
            else if (response.IsSuccessStatusCode)
            {
                TestUrlResult = $"✅ URL phản hồi OK (Status: {(int)response.StatusCode})";
            }
            else
            {
                TestUrlResult = $"⚠️ URL phản hồi lỗi (Status: {(int)response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
            TestUrlResult = $"❌ Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Settings.GoogleDriveEnabled = GoogleDriveEnabled;
        _settingsService.Settings.GoogleDrivePath = GoogleDrivePath;
        _settingsService.Settings.AppsScriptUrl = AppsScriptUrl;
        _settingsService.Settings.EnablePrinting = EnablePrinting;
        _settingsService.Settings.PrinterName = PrinterName;
        _settingsService.Settings.CountdownSeconds = CountdownSeconds;
        _settingsService.Settings.EventName = EventName;

        if (_settingsService.Save())
        {
            StatusMessage = "✅ Đã lưu cài đặt thành công!";
        }
        else
        {
            StatusMessage = "❌ Lỗi lưu cài đặt";
        }
    }
}
