using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoBooth.Admin.Services;

/// <summary>
/// Manages the lifecycle of the PhotoBooth.API background process.
/// When running as a packaged .app, finds and starts the bundled API binary.
/// Falls back gracefully when running in dev mode (dotnet run).
/// </summary>
public class ApiProcessManager : IDisposable
{
    private Process? _apiProcess;
    private bool _disposed;

    public bool IsRunning => _apiProcess != null && !_apiProcess.HasExited;
    public string StatusMessage { get; private set; } = "";

    /// <summary>
    /// Attempts to start the bundled API binary.
    /// Returns true if started successfully or already running on port 5148.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        // Check if API is already up (e.g., dev mode with dotnet run)
        if (await IsApiAlreadyRunningAsync())
        {
            StatusMessage = "[API] Already running on port 5148";
            Console.WriteLine(StatusMessage);
            return true;
        }

        var apiBinaryPath = FindApiBinary();
        if (apiBinaryPath == null)
        {
            StatusMessage = "[API] Binary not found — assuming dev mode";
            Console.WriteLine(StatusMessage);
            return false;
        }

        try
        {
            var apiDir = Path.GetDirectoryName(apiBinaryPath)!;

            var startInfo = new ProcessStartInfo
            {
                FileName = apiBinaryPath,
                WorkingDirectory = apiDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                // Pass ASPNETCORE env so it picks up appsettings.Production.json
                Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Production" }
            };

            _apiProcess = Process.Start(startInfo);
            if (_apiProcess == null)
            {
                StatusMessage = "[API] Failed to start process";
                Console.WriteLine(StatusMessage);
                return false;
            }

            Console.WriteLine($"[API] Started PID={_apiProcess.Id} from {apiBinaryPath}");

            // Wait up to 8 seconds for API to become responsive
            for (int i = 0; i < 16; i++)
            {
                await Task.Delay(500);
                if (_apiProcess.HasExited)
                {
                    StatusMessage = $"[API] Exited early (code {_apiProcess.ExitCode})";
                    Console.WriteLine(StatusMessage);
                    return false;
                }
                if (await IsApiAlreadyRunningAsync())
                {
                    StatusMessage = "[API] Ready on http://localhost:5148";
                    Console.WriteLine(StatusMessage);
                    return true;
                }
            }

            StatusMessage = "[API] Started but not yet responding (timeout)";
            Console.WriteLine(StatusMessage);
            return true; // Process is running, might just be slow
        }
        catch (Exception ex)
        {
            StatusMessage = $"[API] Error starting: {ex.Message}";
            Console.WriteLine(StatusMessage);
            return false;
        }
    }

    public void Stop()
    {
        if (_apiProcess == null) return;
        try
        {
            if (!_apiProcess.HasExited)
            {
                Console.WriteLine($"[API] Stopping PID={_apiProcess.Id}");
                _apiProcess.Kill(entireProcessTree: true);
                _apiProcess.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Error stopping: {ex.Message}");
        }
        finally
        {
            _apiProcess.Dispose();
            _apiProcess = null;
        }
    }

    /// <summary>
    /// Find the API binary relative to the Admin executable.
    /// In .app bundle: Contents/MacOS/api/PhotoBooth.API
    /// </summary>
    private static string? FindApiBinary()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Candidate paths relative to the admin binary location
        var candidates = new[]
        {
            Path.Combine(baseDir, "api", "PhotoBooth.API"),
            Path.Combine(baseDir, "..", "api", "PhotoBooth.API"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                Console.WriteLine($"[API] Found binary at: {fullPath}");
                return fullPath;
            }
        }

        Console.WriteLine($"[API] Binary not found. BaseDir={baseDir}");
        return null;
    }

    private static async Task<bool> IsApiAlreadyRunningAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var http = new System.Net.Http.HttpClient();
            var response = await http.GetAsync("http://localhost:5148/api/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
