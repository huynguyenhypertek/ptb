using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    public SessionListViewModel(ApiService apiService)
    {
        _apiService = apiService;
        LoadSessionsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadSessions()
    {
        IsLoading = true;
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
}
