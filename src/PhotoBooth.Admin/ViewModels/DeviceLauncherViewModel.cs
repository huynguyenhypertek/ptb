using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
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

    public bool IsEventMode { get; set; }

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Đã dừng";

    [ObservableProperty]
    private string _statusIcon = "⏹";

    public Process? Process { get; set; }

    public string DisplayName => IsEventMode ? $"🎉 {User.Username} (Sự kiện)" : $"📷 {User.Username}";
    public string StoreDisplay => string.IsNullOrEmpty(StoreName) ? "—" : $"🏪 {StoreName}";
    public string EnabledDisplay => User.IsEnabled ? "🟢 Hoạt động" : "🔴 Đã khóa";
}

public partial class DeviceLauncherViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<DeviceLaunchItem> _devices = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    // Role-based info
    public bool IsSystemAdmin => _apiService.Role == "SystemAdmin";

    public DeviceLauncherViewModel(ApiService apiService, SettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
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
                var existingMap = Devices.ToDictionary(d => $"{d.User.Id}_{(d.IsEventMode ? "Event" : "Normal")}", d => d);

                Devices.Clear();
                foreach (var user in deviceUsers)
                {
                    var storeName = stores?.FirstOrDefault(s => s.Id == user.StoreId)?.Name ?? "";

                    void AddItem(bool isEvent)
                    {
                        var item = new DeviceLaunchItem
                        {
                            User = user,
                            StoreName = storeName,
                            IsEventMode = isEvent
                        };

                        var key = $"{user.Id}_{(isEvent ? "Event" : "Normal")}";
                        if (existingMap.TryGetValue(key, out var existing) && existing.Process != null)
                        {
                            try
                            {
                                if (!existing.Process.HasExited)
                                {
                                    item.Process = existing.Process;
                                    item.IsRunning = true;
                                    item.StatusText = "Đang chạy";
                                    item.StatusIcon = "🟢";
                                    _ = MonitorProcessAsync(item, existing.Process);
                                }
                            }
                            catch { /* Process already exited */ }
                        }
                        Devices.Add(item);
                    }

                    AddItem(false); // Normal mode
                    AddItem(true);  // Event mode
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
            var startInfo = CreateLaunchStartInfo(item);
            if (startInfo == null) return;

            var process = Process.Start(startInfo);
            if (process != null)
            {
                item.Process = process;
                item.IsRunning = true;
                item.StatusText = "Đang chạy";
                item.StatusIcon = "🟢";
                StatusMessage = $"▶️ Đã khởi động {item.User.Username} (PID: {process.Id})";
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

    /// <summary>
    /// Creates the ProcessStartInfo for the device. Exposed for unit testing.
    /// </summary>
    public ProcessStartInfo? CreateLaunchStartInfo(DeviceLaunchItem item)
    {
        // Build args string (same for both packaged and dev modes)
        var argsList = BuildLaunchArgs(item);

        ProcessStartInfo startInfo;

        // --- Packaged mode: launch binary inside .app bundle directly ---
        // macOS reads Info.plist of the .app bundle regardless of how binary is launched,
        // so camera permission works. Using direct launch (not 'open') lets us track the process.
        var binaryPath = item.IsEventMode ? FindEventBinary() : FindUiBinary();
        if (binaryPath != null)
        {
            startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = argsList,
                WorkingDirectory = Path.GetDirectoryName(binaryPath),
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            Console.WriteLine($"[LAUNCHER] Packaged mode: {binaryPath} {argsList}");
        }
        else
        {
            // --- Dev mode: use dotnet run ---
            var dotnetPath = ResolveDotnetPath();
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var srcDir = FindSrcDirectory(currentDir);
            var projectFolderName = item.IsEventMode ? "PhotoBooth.Event" : "PhotoBooth.UI";
            var projectPath = Path.Combine(srcDir, projectFolderName);

            if (!Directory.Exists(projectPath))
            {
                StatusMessage = $"❌ Không tìm thấy {projectFolderName} tại: {projectPath}";
                return null;
            }

            startInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = $"run --project \"{projectPath}\" -- {argsList}",
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            Console.WriteLine($"[LAUNCHER] Dev mode: dotnet run {projectPath}");
        }

        return startInfo;
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

        // BUG FIX (Bug 2): Update observable properties on the UI thread.
        // Raising PropertyChanged from a background thread violates Avalonia's threading rules
        // and can cause cross-thread exceptions in the Admin.
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            item.Process = null;
            item.IsRunning = false;
            item.StatusText = "Đã dừng";
            item.StatusIcon = "⏹";
        });
    }

    private string BuildLaunchArgs(DeviceLaunchItem item)
    {
        var settings = _settingsService.Settings;
        var args = $"--deviceId={item.User.Username} --apiBaseUrl={_apiService.BaseUrl}";

        if (item.User.StoreId.HasValue)
            args += $" --storeId={item.User.StoreId.Value}";

        args += $" --googleDriveEnabled={settings.GoogleDriveEnabled}";
        if (!string.IsNullOrEmpty(settings.GoogleDrivePath))
            args += $" --googleDrivePath=\"{settings.GoogleDrivePath}\"";
        if (!string.IsNullOrEmpty(settings.AppsScriptUrl))
            args += $" --appsScriptUrl={settings.AppsScriptUrl}";
        args += $" --enablePrinting={settings.EnablePrinting}";
        if (!string.IsNullOrEmpty(settings.PrinterName))
            args += $" --printerName=\"{settings.PrinterName}\"";
        
        args += $" --countdownSeconds={settings.CountdownSeconds}";
        if (!string.IsNullOrEmpty(settings.EventName))
            args += $" --eventName=\"{settings.EventName}\"";

        return args;
    }

    /// <summary>
    /// Find the PhotoBooth.UI binary inside the bundled .app structure.
    /// Path: MacOS/ui/PhotoBooth.UI.app/Contents/MacOS/PhotoBooth.UI
    /// Returns null when running in dev mode.
    /// </summary>
    private static string? FindUiBinary()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            // Packaged: MacOS/ui/PhotoBooth.UI.app/Contents/MacOS/PhotoBooth.UI
            Path.Combine(baseDir, "ui", "PhotoBooth.UI.app", "Contents", "MacOS", "PhotoBooth.UI"),
            Path.Combine(baseDir, "..", "ui", "PhotoBooth.UI.app", "Contents", "MacOS", "PhotoBooth.UI"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                Console.WriteLine($"[LAUNCHER] Found UI binary: {fullPath}");
                return fullPath;
            }
        }
        return null;
    }

    /// <summary>
    /// Find the PhotoBooth.Event binary inside the bundled .app structure.
    /// Path: MacOS/event/PhotoBooth.Event.app/Contents/MacOS/PhotoBooth.Event
    /// </summary>
    private static string? FindEventBinary()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "event", "PhotoBooth.Event.app", "Contents", "MacOS", "PhotoBooth.Event"),
            Path.Combine(baseDir, "..", "event", "PhotoBooth.Event.app", "Contents", "MacOS", "PhotoBooth.Event"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                Console.WriteLine($"[LAUNCHER] Found Event binary: {fullPath}");
                return fullPath;
            }
        }
        return null;
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
                
            if (Directory.Exists(Path.Combine(dir, "src", "PhotoBooth.UI")))
                return Path.Combine(dir, "src");
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
