using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

/// <summary>
/// Represents a launchable device with its process state
/// </summary>
public partial class DeviceLaunchItem : ObservableObject
{
    public UserItem User { get; set; } = new();
    public string StoreName { get; set; } = "";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Đã dừng";

    [ObservableProperty]
    private string _statusIcon = "⏹";

    public Process? Process { get; set; }

    public string DisplayName => $"📷 {User.Username}";
    public string StoreDisplay => string.IsNullOrEmpty(StoreName) ? "—" : $"🏪 {StoreName}";
    public string EnabledDisplay => User.IsEnabled ? "🟢 Hoạt động" : "🔴 Đã khóa";
}

public partial class DeviceLauncherViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<DeviceLaunchItem> _devices = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    // Role-based info
    public bool IsSystemAdmin => _apiService.Role == "SystemAdmin";

    public DeviceLauncherViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _ = LoadDevicesAsync();
    }

    [RelayCommand]
    private async Task LoadDevicesAsync()
    {
        IsLoading = true;
        StatusMessage = "";
        try
        {
            // Fetch users - filter by store for StoreAdmin
            var storeId = _apiService.Role == "StoreAdmin" ? _apiService.StoreId : null;
            var users = await _apiService.GetUsersAsync(storeId);

            // Fetch stores for name lookup
            StoreItem[]? stores = null;
            try { stores = await _apiService.GetStoresAsync(); } catch { }

            if (users != null)
            {
                // Only show Device-role accounts
                var deviceUsers = users.Where(u => u.Role == "Device").ToList();

                // Preserve running processes from existing items
                var existingMap = Devices.ToDictionary(d => d.User.Id, d => d);

                Devices.Clear();
                foreach (var user in deviceUsers)
                {
                    var item = new DeviceLaunchItem
                    {
                        User = user,
                        StoreName = stores?.FirstOrDefault(s => s.Id == user.StoreId)?.Name ?? ""
                    };

                    // Restore process state if device was previously running
                    if (existingMap.TryGetValue(user.Id, out var existing) && existing.Process != null)
                    {
                        try
                        {
                            if (!existing.Process.HasExited)
                            {
                                item.Process = existing.Process;
                                item.IsRunning = true;
                                item.StatusText = "Đang chạy";
                                item.StatusIcon = "🟢";
                            }
                        }
                        catch { /* Process already exited */ }
                    }

                    Devices.Add(item);
                }

                StatusMessage = $"✅ {deviceUsers.Count} thiết bị";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi tải danh sách: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void LaunchDevice(DeviceLaunchItem? item)
    {
        if (item == null) return;

        if (item.IsRunning)
        {
            // Already running
            StatusMessage = $"⚠️ {item.User.Username} đang chạy rồi";
            return;
        }

        if (!item.User.IsEnabled)
        {
            StatusMessage = $"🔴 {item.User.Username} đã bị khóa, không thể khởi động";
            return;
        }

        try
        {
            // Resolve dotnet path
            var dotnetPath = ResolveDotnetPath();
            
            // Resolve PhotoBooth.UI project path relative to this solution
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            // Walk up to find src directory
            var srcDir = FindSrcDirectory(currentDir);
            var uiProjectPath = Path.Combine(srcDir, "PhotoBooth.UI");

            if (!Directory.Exists(uiProjectPath))
            {
                StatusMessage = $"❌ Không tìm thấy PhotoBooth.UI tại: {uiProjectPath}";
                return;
            }

            // Build launch arguments
            var args = $"run --project \"{uiProjectPath}\" -- " +
                       $"--deviceId={item.User.Username} " +
                       $"--apiBaseUrl={_apiService.BaseUrl}";

            if (item.User.StoreId.HasValue)
                args += $" --storeId={item.User.StoreId.Value}";

            var startInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                item.Process = process;
                item.IsRunning = true;
                item.StatusText = "Đang chạy";
                item.StatusIcon = "🟢";

                StatusMessage = $"▶️ Đã khởi động {item.User.Username} (PID: {process.Id})";

                // Monitor process exit in background
                _ = MonitorProcessAsync(item, process);
            }
            else
            {
                StatusMessage = $"❌ Không thể khởi động {item.User.Username}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi khởi động: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopDevice(DeviceLaunchItem? item)
    {
        if (item == null || !item.IsRunning || item.Process == null) return;

        try
        {
            if (!item.Process.HasExited)
            {
                item.Process.Kill(entireProcessTree: true);
                item.Process.WaitForExit(3000);
            }

            item.Process = null;
            item.IsRunning = false;
            item.StatusText = "Đã dừng";
            item.StatusIcon = "⏹";

            StatusMessage = $"⏸ Đã dừng {item.User.Username}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi dừng: {ex.Message}";
            // Force cleanup state
            item.IsRunning = false;
            item.StatusText = "Đã dừng";
            item.StatusIcon = "⏹";
            item.Process = null;
        }
    }

    [RelayCommand]
    private async Task ToggleLockDevice(DeviceLaunchItem? item)
    {
        if (item == null) return;
        IsLoading = true;
        try
        {
            var success = await _apiService.ToggleUserEnabledAsync(item.User.Id);
            if (success)
            {
                var newStatus = item.User.IsEnabled ? "🔴 Đã khóa" : "🟢 Kích hoạt";
                StatusMessage = $"✅ {item.User.Username} → {newStatus}";
                await LoadDevicesAsync();
            }
            else
            {
                StatusMessage = "❌ Lỗi thay đổi trạng thái";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task MonitorProcessAsync(DeviceLaunchItem item, Process process)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch { }

        // Update UI on process exit
        item.Process = null;
        item.IsRunning = false;
        item.StatusText = "Đã dừng";
        item.StatusIcon = "⏹";
    }

    private static string ResolveDotnetPath()
    {
        // Try common locations
        var candidates = new[]
        {
            // macOS/Linux custom installs
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet"),
            // System installs
            "/usr/local/share/dotnet/dotnet",
            "/usr/share/dotnet/dotnet",
            // Windows
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Fallback: assume it's in PATH
        return "dotnet";
    }

    private static string FindSrcDirectory(string startDir)
    {
        var dir = startDir;
        // Walk up until we find a directory containing PhotoBooth.UI
        for (int i = 0; i < 10; i++)
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
            
            if (Directory.Exists(Path.Combine(dir, "PhotoBooth.UI")))
                return dir;
        }

        // Fallback: try to find from current assembly location
        var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        dir = assemblyDir;
        for (int i = 0; i < 10; i++)
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
            
            if (Directory.Exists(Path.Combine(dir, "PhotoBooth.UI")))
                return dir;
        }

        // Last resort
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..");
    }

    /// <summary>
    /// Cleanup: Kill all running processes when the ViewModel is being discarded
    /// </summary>
    public void StopAllDevices()
    {
        foreach (var item in Devices.Where(d => d.IsRunning))
        {
            try
            {
                if (item.Process != null && !item.Process.HasExited)
                {
                    item.Process.Kill(entireProcessTree: true);
                }
            }
            catch { }
            finally
            {
                item.IsRunning = false;
                item.Process = null;
            }
        }
    }
}
