using System;
using System.IO;
using System.Text.Json;

namespace PhotoBooth.Admin.Services;

public class AppSettings
{
    public bool GoogleDriveEnabled { get; set; } = true;
    public string GoogleDrivePath { get; set; } = "";
    public string AppsScriptUrl { get; set; } = "";
    public bool EnablePrinting { get; set; } = false;
    public string PrinterName { get; set; } = "";
    /// <summary>
    /// Khổ giấy CUPS (PPD PageSize). DNP DS-RX1/RX1HS khổ 10x15cm = "300dnp6x4".
    /// Bỏ trống = dùng khổ mặc định của driver (DNP mặc định là 310dnp6x8, KHÔNG phải 6x4).
    /// </summary>
    public string PrintMedia { get; set; } = "300dnp6x4";
    public int CountdownSeconds { get; set; } = 3;
    public string EventName { get; set; } = "DONGFEST";
}

public class SettingsService
{
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        // Store settings.json next to the executable
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _settingsFilePath = Path.Combine(baseDir, "settings.json");
        Load();
    }

    /// <summary>
    /// Load settings from JSON file. If file doesn't exist, keep defaults.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Settings = loaded;
                }
                Console.WriteLine($"[SETTINGS] Loaded from: {_settingsFilePath}");
            }
            else
            {
                Console.WriteLine($"[SETTINGS] No settings file found, using defaults");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SETTINGS] Error loading: {ex.Message}");
        }
    }

    /// <summary>
    /// Save current settings to JSON file.
    /// </summary>
    public bool Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            Console.WriteLine($"[SETTINGS] Saved to: {_settingsFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SETTINGS] Error saving: {ex.Message}");
            return false;
        }
    }
}
