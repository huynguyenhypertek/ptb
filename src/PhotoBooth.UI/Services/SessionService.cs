using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PhotoBooth.Core.Models;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Manages the current session state across all views.
/// </summary>
public class SessionService
{
    /// <summary>
    /// Base directory for all session folders.
    /// Returns Google Drive path when enabled and accessible, otherwise ~/Pictures/PhotoBooth/.
    /// Validates Directory.Exists so fallback is consistent across SessionService and CaptureViewModel.
    /// </summary>
    private static string SessionsBaseDirectory
    {
        get
        {
            if (DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.GoogleDrivePath))
            {
                if (Directory.Exists(DeviceConfig.GoogleDrivePath))
                {
                    return DeviceConfig.GoogleDrivePath;
                }
                Console.WriteLine($"[WARNING] Google Drive path not found: {DeviceConfig.GetMaskedGDrivePath()} — falling back to local storage");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoBooth");
        }
    }

    /// <summary>
    /// Any directory creation date before this is considered invalid/unsupported.
    /// Windows returns 1601-01-01 (FILETIME epoch), Linux returns 0001-01-01 (DateTime.MinValue)
    /// when CreationTimeUtc is not supported — both are safely caught by this threshold.
    /// </summary>
    private static readonly DateTime MinValidDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Session CurrentSession { get; private set; } = new();

    public void StartNewSession()
    {
        CurrentSession = new Session();
    }

    /// <summary>
    /// Creates the session directory on disk early (before capture starts).
    /// Call this as soon as layout/background are selected so Google Drive Desktop
    /// has more time to detect and sync the empty folder to the cloud.
    /// Also starts background pre-fetch of Google Drive URL for instant QR generation.
    /// </summary>
    public void PrepareSessionDirectory()
    {
        var sessionFolder = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..6]}";
        var basePath = GetSessionsBaseDirectory();
        var fullPath = Path.Combine(basePath, sessionFolder);
        Directory.CreateDirectory(fullPath);

        CurrentSession.SessionDirectory = fullPath;
        CurrentSession.SessionFolderName = sessionFolder;

        Console.WriteLine($"[SESSION] Directory prepared early: {sessionFolder}");

        // Gán mã số thứ tự cho session (chỉ gán nếu chưa có — tránh double-assignment)
        if (string.IsNullOrEmpty(CurrentSession.SequentialNumber))
        {
            try
            {
                CurrentSession.SequentialNumber = SequentialNumberService.GetNextNumber();
                Console.WriteLine($"[SESSION] Sequential number: {CurrentSession.SequentialNumber}");
            }
            catch (Exception ex)
            {
                // F1: SaveState now throws on persistence failure (disk full, permissions, etc.)
                // Session continues without sequential number — better than crash or duplicate
                Console.WriteLine($"[SESSION] WARNING: Failed to assign sequential number: {ex.Message}");
            }
        }

        // Start background pre-fetch of Google Drive URL during payment/capture screens
        if (DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.AppsScriptUrl))
        {
            _ = PreFetchDriveUrlAsync(sessionFolder);
        }
    }

    /// <summary>
    /// Background task: polls Apps Script until folder appears on Google Drive,
    /// then caches the URL in Session.PreFetchedDriveUrl for instant QR generation.
    /// Runs during payment/capture screens (~20-30s before QR screen).
    /// </summary>
    private async Task PreFetchDriveUrlAsync(string sessionFolderName)
    {
        const int maxAttempts = 20; // More attempts since we have more time
        const int delayMs = 3000;

        var client = HttpService.Client;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var separator = DeviceConfig.AppsScriptUrl.Contains('?') ? "&" : "?";
                var url = $"{DeviceConfig.AppsScriptUrl}{separator}folder={Uri.EscapeDataString(sessionFolderName)}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var json = await client.GetStringAsync(url, cts.Token);
                var result = System.Text.Json.JsonSerializer.Deserialize<AppsScriptResponse>(json);

                if (result?.Success == true && !string.IsNullOrEmpty(result.Url))
                {
                    CurrentSession.PreFetchedDriveUrl = result.Url;
                    Console.WriteLine($"[SESSION] Pre-fetched Drive URL at attempt {attempt}");
                    return;
                }

                Console.WriteLine($"[SESSION] Pre-fetch attempt {attempt}: folder not on cloud yet");
            }
            catch (Exception)
            {
                Console.WriteLine($"[SESSION] Pre-fetch attempt {attempt}: request failed");
            }

            await Task.Delay(delayMs);
        }

        Console.WriteLine("[SESSION] Pre-fetch exhausted — ThankYou will retry");
    }

    public void SetLayout(Layout layout)
    {
        CurrentSession.SelectedLayout = layout;
    }

    public void SetFrame(Frame frame)
    {
        CurrentSession.SelectedFrame = frame;
    }

    public void SetBackground(Background background)
    {
        CurrentSession.SelectedBackground = background;
    }

    public void AddCapturedPhoto(string path)
    {
        CurrentSession.CapturedPhotoPaths.Add(path);
    }

    public void SetSelectedPhotos(List<int> indices)
    {
        CurrentSession.SelectedPhotoIndices = indices;
    }

    public void AddSticker(StickerPlacement sticker)
    {
        CurrentSession.Stickers.Add(sticker);
    }

    public void SetPaymentComplete(bool isPaid)
    {
        CurrentSession.IsPaid = isPaid;
    }

    public void SetFinalImage(string path)
    {
        CurrentSession.FinalImagePath = path;
    }

    public void SetQRCode(string url)
    {
        CurrentSession.QRCodeUrl = url;
    }

    /// <summary>
    /// Returns the base directory where all session folders are stored.
    /// </summary>
    public static string GetSessionsBaseDirectory() => SessionsBaseDirectory;

    /// <summary>
    /// Returns the best available UTC timestamp for the directory's age.
    /// Falls back to LastWriteTimeUtc when CreationTimeUtc returns the epoch sentinel
    /// (happens on Linux ext4 which doesn't support birth time — Finding 6).
    /// </summary>
    private static DateTime GetDirectoryAgeUtc(string dir)
    {
        var created = Directory.GetCreationTimeUtc(dir);
        if (created < MinValidDate)
        {
            // CreationTime is unsupported (Win: 1601, Linux: 0001) — fall back to last-write time
            return Directory.GetLastWriteTimeUtc(dir);
        }
        return created;
    }

    /// <summary>
    /// Scans the sessions base directory and deletes subdirectories older than <paramref name="maxAge"/>.
    /// Safe to call at startup before any session is active.
    /// </summary>
    public static void CleanupStaleSessions(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(SessionsBaseDirectory))
                return;

            var cutoff = DateTime.UtcNow - maxAge;
            var dirs = Directory.GetDirectories(SessionsBaseDirectory);
            int cleaned = 0;

            foreach (var dir in dirs)
            {
                try
                {
                    // Finding 6: Use fallback-aware helper for Linux compat
                    var dirAge = GetDirectoryAgeUtc(dir);
                    if (dirAge < cutoff)
                    {
                        Directory.Delete(dir, recursive: true);
                        cleaned++;
                        Console.WriteLine($"[CLEANUP] Stale session removed: {Path.GetFileName(dir)} (age {dirAge:yyyy-MM-dd HH:mm} UTC)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLEANUP] Failed to remove stale dir {Path.GetFileName(dir)}: {ex.Message}");
                }
            }

            if (cleaned > 0)
                Console.WriteLine($"[CLEANUP] Removed {cleaned} stale session(s) at startup");
            else
                Console.WriteLine($"[CLEANUP] No stale sessions found (checked {dirs.Length} dir(s))");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEANUP] Startup cleanup error: {ex.Message}");
        }
    }
}
