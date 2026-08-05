using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoBooth.Event.Services;

// WARNING: This is a copy of PhotoBooth.UI/Services/SequentialNumberService.cs (namespace change only).
// Both share ~/.photobooth/counter.json at runtime. Keep logic in sync across both copies.

/// <summary>
/// Manages sequential numbers for each photo session.
/// Format: MMDD-NNN (e.g., 0516-001, 0516-042)
/// Counter persisted via JSON file, auto-resets daily.
/// Thread-safe (single-process scope only).
/// </summary>
public static class SequentialNumberService
{
    private static readonly object _lock = new();

    // Cache JsonSerializerOptions — expensive to construct on every call
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // internal so test project can override via InternalsVisibleTo
    internal static string CounterFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".photobooth",
        "counter.json"
    );

    /// <summary>
    /// Returns the next sequential number. Thread-safe, auto-resets on new day.
    /// Format D3 zero-padded (001-999), auto-expands if > 999.
    /// </summary>
    public static string GetNextNumber()
    {
        lock (_lock)
        {
            var state = LoadState();
            // NOTE: Uses DateTime.Now (local time) intentionally — MMDD prefix is for
            // human-readable local identification. session.json uses UTC for audit/debug.
            var today = DateTime.Now.ToString("MMdd");

            // Reset counter on new day
            if (state.DatePrefix != today)
            {
                state.DatePrefix = today;
                state.Counter = 0;
            }

            state.Counter++;

            // Let SaveState throw — caller must know if persistence failed
            // to avoid duplicate numbers on next call
            SaveState(state);

            return $"{state.DatePrefix}-{state.Counter:D3}";
        }
    }

    /// <summary>
    /// Returns the next number that will be assigned (without incrementing). Used for preview display.
    /// Note: If it's a new day with no sessions yet, returns the first number that will be assigned.
    /// </summary>
    public static string PeekNextNumber()
    {
        lock (_lock)
        {
            var state = LoadState();
            var today = DateTime.Now.ToString("MMdd");

            if (state.DatePrefix != today)
                return $"{today}-001"; // New day — next assigned will be 001

            if (state.Counter == 0)
                return $"{today}-001"; // No sessions today yet

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

                // Validate deserialized state — guard against tampered/corrupt values
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
    /// Throws on failure — caller (GetNextNumber) must not return a number
    /// that wasn't persisted, otherwise the next call would produce a duplicate.
    /// Uses atomic write (temp file + rename) to prevent corruption on crash/power loss.
    /// </summary>
    private static void SaveState(CounterState state)
    {
        var dir = Path.GetDirectoryName(CounterFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(state, _jsonOptions);

        // Atomic write — write to temp then rename to avoid partial writes on crash
        var tempPath = CounterFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, CounterFilePath, overwrite: true);
    }
}

// Internal — implementation detail, tests access via InternalsVisibleTo
internal class CounterState
{
    [JsonPropertyName("datePrefix")]
    public string DatePrefix { get; set; } = "";

    [JsonPropertyName("counter")]
    public int Counter { get; set; } = 0;
}
