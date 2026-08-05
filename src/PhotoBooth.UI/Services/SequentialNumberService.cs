using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Quản lý mã số thứ tự cho mỗi session chụp ảnh.
/// Format: MMDD-NNN (ví dụ: 0516-001, 0516-042)
/// Counter persistent qua file JSON, auto-reset mỗi ngày.
/// Thread-safe (single-process scope only).
/// </summary>
public static class SequentialNumberService
{
    private static readonly object _lock = new();

    // F2: Cache JsonSerializerOptions — expensive to construct on every call
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // internal để test project có thể override qua InternalsVisibleTo
    internal static string CounterFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".photobooth",
        "counter.json"
    );

    /// <summary>
    /// Lấy mã số tiếp theo. Thread-safe, auto-reset khi sang ngày mới.
    /// Format D3 pad zero (001-999), tự mở rộng nếu > 999.
    /// </summary>
    public static string GetNextNumber()
    {
        lock (_lock)
        {
            var state = LoadState();
            // NOTE: Uses DateTime.Now (local time) intentionally — MMDD prefix is for
            // human-readable local identification. session.json uses UTC for audit/debug.
            var today = DateTime.Now.ToString("MMdd");

            // Reset counter nếu sang ngày mới
            if (state.DatePrefix != today)
            {
                state.DatePrefix = today;
                state.Counter = 0;
            }

            state.Counter++;

            // F1: Let SaveState throw — caller must know if persistence failed
            // to avoid duplicate numbers on next call
            SaveState(state);

            return $"{state.DatePrefix}-{state.Counter:D3}";
        }
    }

    /// <summary>
    /// Lấy mã số dự kiến sẽ được gán (không tăng counter). Dùng để hiển thị preview.
    /// Lưu ý: Nếu là ngày mới và chưa chụp, trả về số tiếp theo sẽ được gán.
    /// </summary>
    public static string PeekNextNumber()
    {
        lock (_lock)
        {
            var state = LoadState();
            var today = DateTime.Now.ToString("MMdd");

            if (state.DatePrefix != today)
                return $"{today}-001"; // Preview: số tiếp theo sẽ được gán

            if (state.Counter == 0)
                return $"{today}-001"; // Chưa có session nào hôm nay

            return $"{state.DatePrefix}-{(state.Counter + 1):D3}";
        }
    }

    private static CounterState LoadState()
    {
        try
        {
            if (File.Exists(CounterFilePath))
            {
                var json = File.ReadAllText(CounterFilePath);
                var state = JsonSerializer.Deserialize<CounterState>(json) ?? new CounterState();

                // A1: Validate deserialized state — guard against tampered/corrupt values
                // Negative counter would produce invalid numbers like "MMDD-000" or "MMDD--005"
                if (state.Counter < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[COUNTER] Invalid counter value {state.Counter} — resetting to 0");
                    state.Counter = 0;
                }
                state.DatePrefix ??= "";

                return state;
            }
        }
        catch (Exception ex)
        {
            // Corrupt/empty file → reset to fresh state (safe recovery)
            System.Diagnostics.Debug.WriteLine($"[COUNTER] Failed to load state (will reset): {ex.Message}");
        }
        return new CounterState();
    }

    /// <summary>
    /// F1: Throws on failure — caller (GetNextNumber) must not return a number
    /// that wasn't persisted, otherwise the next call would produce a duplicate.
    /// F3: Uses atomic write (temp file + rename) to prevent corruption on crash/power loss.
    /// </summary>
    private static void SaveState(CounterState state)
    {
        var dir = Path.GetDirectoryName(CounterFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(state, _jsonOptions);

        // F3: Atomic write — write to temp then rename to avoid partial writes on crash
        var tempPath = CounterFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, CounterFilePath, overwrite: true);
    }
}

// F4: Changed from public to internal — implementation detail, tests access via InternalsVisibleTo
internal class CounterState
{
    [JsonPropertyName("datePrefix")]
    public string DatePrefix { get; set; } = "";

    [JsonPropertyName("counter")]
    public int Counter { get; set; } = 0;
}
