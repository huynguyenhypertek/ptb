using System;
using System.Collections.Generic;
using System.IO;
using PhotoBooth.Core.Models;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Manages the current session state across all views.
/// </summary>
public class SessionService
{
    /// <summary>
    /// Base directory for all session folders: MyPictures/PhotoBooth/
    /// </summary>
    private static readonly string SessionsBaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "PhotoBooth"
    );

    /// <summary>
    /// Any directory creation date before this is considered invalid/unsupported.
    /// Windows returns 1601-01-01 (FILETIME epoch), Linux returns 0001-01-01 (DateTime.MinValue)
    /// when CreationTimeUtc is not supported — both are safely caught by this threshold.
    /// </summary>
    private static readonly DateTime MinValidDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Tracks the previous session's directory path explicitly so cleanup works
    /// even if CurrentSession.SessionDirectory was never set (Finding 3).
    /// </summary>
    private string? _previousSessionDir;

    public Session CurrentSession { get; private set; } = new();

    public void StartNewSession()
    {
        CurrentSession = new Session();
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
    /// Deletes the previous session's directory (if it exists and is safe to delete).
    /// Called automatically at the start of StartNewSession().
    /// Skips deletion if FinalImagePath still exists (upload may be in-flight) — defers to stale sweep.
    /// </summary>
    /// <param name="previousFinalImage">The FinalImagePath from the previous session, captured BEFORE CurrentSession is replaced.</param>
    private void CleanupPreviousSession(string? previousFinalImage)
    {
        var sessionDir = _previousSessionDir;
        if (string.IsNullOrEmpty(sessionDir) || !Directory.Exists(sessionDir))
            return;

        // If a final image still exists inside the session dir, an in-flight upload may be reading it.
        // Defer cleanup to the stale-session sweep at next startup.
        if (!string.IsNullOrEmpty(previousFinalImage)
            && File.Exists(previousFinalImage)
            && previousFinalImage.StartsWith(sessionDir, StringComparison.Ordinal))
        {
            Console.WriteLine($"[CLEANUP] Deferred cleanup of {sessionDir} — FinalImagePath still exists (possible in-flight upload)");
            return;
        }

        try
        {
            Directory.Delete(sessionDir, recursive: true);
            Console.WriteLine($"[CLEANUP] Deleted previous session: {sessionDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEANUP] Failed to delete {sessionDir}: {ex.Message}");
        }
    }

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
