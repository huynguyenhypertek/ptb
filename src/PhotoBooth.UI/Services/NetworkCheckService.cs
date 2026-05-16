using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Kiểm tra kết nối internet bằng cách ping Google's generate_204 endpoint.
/// Kết quả cache 10 giây để tránh gọi liên tục.
/// 
/// A1: Uses shared HttpService.Client — no socket exhaustion risk.
/// A2: Benign data race on _lastResult/_lastCheck is acceptable on x64
///     (boolean + DateTime writes are effectively atomic). Documented.
/// A3: Hardcoded Google endpoint is acceptable — if Google is unreachable,
///     the entire Google Drive flow fails regardless.
/// </summary>
public static class NetworkCheckService
{
    private static readonly object _syncRoot = new();
    private static bool _lastResult = true;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Kiểm tra có kết nối internet không.
    /// Cache kết quả 10 giây.
    /// </summary>
    public static async Task<bool> IsOnlineAsync()
    {
        lock (_syncRoot)
        {
            if ((DateTime.UtcNow - _lastCheck) < CacheDuration)
                return _lastResult;
        }

        bool result;
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response = await HttpService.Client.GetAsync("https://www.google.com/generate_204", cts.Token);
            result = response.IsSuccessStatusCode;
        }
        catch
        {
            result = false;
        }

        lock (_syncRoot)
        {
            _lastResult = result;
            _lastCheck = DateTime.UtcNow;
        }

        Console.WriteLine($"[NETWORK] Online check: {result}");
        return result;
    }

    /// <summary>
    /// Force-reset cache — useful for testing or when a known network event occurs.
    /// </summary>
    internal static void ResetCache()
    {
        lock (_syncRoot)
        {
            _lastCheck = DateTime.MinValue;
            _lastResult = true;
        }
    }
}
