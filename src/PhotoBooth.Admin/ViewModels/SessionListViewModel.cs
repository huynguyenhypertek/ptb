using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class SessionListViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<SessionItem> _sessions = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _totalAmount = "0đ";

    [ObservableProperty]
    private string _exportStatus = "";

    [ObservableProperty]
    private bool _isConfirmingReset;

    public SessionListViewModel(ApiService apiService)
    {
        _apiService = apiService;
        LoadSessionsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadSessions()
    {
        IsLoading = true;
        ExportStatus = "";
        try
        {
            var sessions = await _apiService.GetSessionsAsync();
            if (sessions != null)
            {
                Sessions.Clear();
                foreach (var s in sessions)
                    Sessions.Add(s);
                
                TotalAmount = sessions.Sum(s => s.Amount).ToString("N0") + "đ";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading sessions: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            if (Sessions.Count == 0)
            {
                ExportStatus = "⚠️ Không có dữ liệu để xuất";
                return;
            }

            // Save to Desktop with timestamp
            var fileName = $"luot_chup_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                fileName);

            // UTF-8 BOM — required for Excel to open Vietnamese correctly
            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            // Header
            writer.WriteLine("ID,Thiết bị,Layout,Khung,Ảnh chọn,Tổng chụp,Số tiền (đ),Thời gian");

            // Rows
            foreach (var s in Sessions)
            {
                // Escape fields containing commas or quotes
                writer.WriteLine(
                    $"{s.Id}," +
                    $"{EscapeCsv(s.DeviceId)}," +
                    $"{EscapeCsv(s.LayoutUsed)}," +
                    $"{EscapeCsv(s.FrameUsed)}," +
                    $"{s.PhotoCount}," +
                    $"{s.TotalCaptured}," +
                    $"{s.Amount}," +
                    $"{s.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}");
            }

            ExportStatus = $"✅ Đã xuất {Sessions.Count} lượt → Desktop/{fileName}";
            Console.WriteLine($"[EXPORT] CSV saved: {filePath}");
        }
        catch (Exception ex)
        {
            ExportStatus = $"❌ Lỗi xuất file: {ex.Message}";
            Console.WriteLine($"[EXPORT] Error: {ex.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // Step 1: Show confirmation bar
    [RelayCommand]
    private void RequestReset()
    {
        IsConfirmingReset = true;
        ExportStatus = "";
    }

    // Step 2: Actually delete all
    [RelayCommand]
    private async Task ConfirmReset()
    {
        IsConfirmingReset = false;
        IsLoading = true;
        try
        {
            var (success, message) = await _apiService.ResetAllSessionsAsync();
            ExportStatus = success ? $"✅ {message}" : $"❌ {message}";
            if (success)
                await LoadSessions(); // reload empty list
        }
        catch (Exception ex)
        {
            ExportStatus = $"❌ Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Step 2 (cancel): Dismiss confirmation bar
    [RelayCommand]
    private void CancelReset()
    {
        IsConfirmingReset = false;
    }
}
