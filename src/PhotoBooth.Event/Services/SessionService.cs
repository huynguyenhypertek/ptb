using System;
using System.Collections.Generic;
using System.IO;
using PhotoBooth.Core.Models;

namespace PhotoBooth.Event.Services;

/// <summary>
/// Manages the current session state across all views.
/// Simplified for event flow — no payment, sticker, layout, or Google Drive pre-fetch.
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
    /// Assigns sequential number for session identification.
    /// </summary>
    public void PrepareSessionDirectory()
    {
        var sessionFolder = $"{DateTime.Now:yyMMdd_HHmmss}";
        var basePath = GetSessionsBaseDirectory();
        var fullPath = Path.Combine(basePath, sessionFolder);
        Directory.CreateDirectory(fullPath);

        CurrentSession.SessionDirectory = fullPath;
        CurrentSession.SessionFolderName = sessionFolder;

        Console.WriteLine($"[SESSION] Directory prepared early: {sessionFolder}");

        // Assign sequential number for session (only if not already assigned — avoid double-assignment)
        if (string.IsNullOrEmpty(CurrentSession.SequentialNumber))
        {
            try
            {
                CurrentSession.SequentialNumber = SequentialNumberService.GetNextNumber();
                Console.WriteLine($"[SESSION] Sequential number: {CurrentSession.SequentialNumber}");
            }
            catch (Exception ex)
            {
                // SaveState throws on persistence failure (disk full, permissions, etc.)
                // Session continues without sequential number — better than crash or duplicate
                Console.WriteLine($"[SESSION] WARNING: Failed to assign sequential number: {ex.Message}");
            }
        }
    }

    public void AddCapturedPhoto(string path)
    {
        CurrentSession.CapturedPhotoPaths.Add(path);
    }

    public void SetSelectedPhotos(List<int> indices)
    {
        CurrentSession.SelectedPhotoIndices = indices;
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
    /// (happens on Linux ext4 which doesn't support birth time).
    /// </summary>
    private static DateTime GetDirectoryAgeUtc(string dir)
    {
        var created = Directory.GetCreationTimeUtc(dir);
        if (created < MinValidDate)
        {
            // CreationTime unsupported (Win: 1601, Linux: 0001) — fall back to last-write time
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
                    // Use fallback-aware helper for Linux compat
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
