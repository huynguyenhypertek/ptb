using System;
using System.Net.Http;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Shared HttpClient to avoid socket exhaustion.
/// Uses Lazy initialization for thread-safe singleton pattern.
/// 
/// IMPORTANT RULES:
/// 1. Do NOT dispose this client — it is shared across the entire app lifetime.
/// 2. Do NOT mutate <see cref="HttpClient.Timeout"/> after construction — it is NOT thread-safe
///    and would affect ALL concurrent callers. 
/// 3. For per-call timeouts, use <c>CancellationTokenSource(TimeSpan)</c>:
///    <code>
///    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
///    var response = await HttpService.Client.GetAsync(url, cts.Token);
///    </code>
/// </summary>
public static class HttpService
{
    private static readonly Lazy<HttpClient> _client = new(() =>
    {
        var client = new HttpClient();
        // Global timeout — do NOT change this at runtime (not thread-safe on shared instance).
        // Use per-call CancellationTokenSource for shorter timeouts on specific requests.
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    });

    public static HttpClient Client => _client.Value;
}
