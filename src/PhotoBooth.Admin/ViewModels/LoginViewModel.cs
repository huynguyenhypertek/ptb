using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly Action<string> _onLoginSuccess;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isLoading;

    public LoginViewModel(ApiService apiService, Action<string> onLoginSuccess)
    {
        _apiService = apiService;
        _onLoginSuccess = onLoginSuccess;
    }

    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập tên đăng nhập và mật khẩu";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var result = await _apiService.LoginAsync(Username, Password);
            if (result != null)
            {
                // Nếu là Device → mở PhotoBooth.UI thay vì Admin dashboard
                if (result.Role == "Device")
                {
                    LaunchPhotoBooth(result);
                    return;
                }
                
                // Admin hoặc StoreAdmin → vào dashboard
                _onLoginSuccess(result.Username);
            }
            else
            {
                ErrorMessage = "Sai tên đăng nhập hoặc mật khẩu";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể kết nối server: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LaunchPhotoBooth(LoginResponse loginResult)
    {
        try
        {
            // Tìm đường dẫn đến PhotoBooth.UI project
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            // Navigate from bin/Debug/net10.0 → src/PhotoBooth.UI
            var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));
            var uiProject = Path.Combine(projectRoot, "src", "PhotoBooth.UI");

            if (!Directory.Exists(uiProject))
            {
                ErrorMessage = $"Không tìm thấy PhotoBooth.UI tại: {uiProject}";
                IsLoading = false;
                return;
            }
            
            // Launch PhotoBooth.UI with storeId and deviceId as args
            var process = new Process();
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"run --project \"{uiProject}\" -- --storeId={loginResult.StoreId} --deviceId={loginResult.Username} --planType={loginResult.PlanType} --apiBaseUrl={_apiService.BaseUrl}";
            process.StartInfo.UseShellExecute = false;
            process.Start();

            Console.WriteLine($"[LAUNCH] PhotoBooth.UI started for {loginResult.Username} (Store={loginResult.StoreId})");
            
            ErrorMessage = $"✅ Đang mở PhotoBooth cho {loginResult.Username}...";
            IsLoading = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi mở PhotoBooth: {ex.Message}";
            IsLoading = false;
        }
    }
}
