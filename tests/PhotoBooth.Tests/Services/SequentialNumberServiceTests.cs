using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PhotoBooth.UI.Services;
using Xunit;

namespace PhotoBooth.Tests.Services;

[Collection("Sequential")]
public class SequentialNumberServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalPath;

    public SequentialNumberServiceTests()
    {
        _originalPath = SequentialNumberService.CounterFilePath;
        _tempDir = Path.Combine(Path.GetTempPath(), $"photobooth_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        SequentialNumberService.CounterFilePath = Path.Combine(_tempDir, "counter.json");
    }

    public void Dispose()
    {
        SequentialNumberService.CounterFilePath = _originalPath;
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void GetNextNumber_Increment_ReturnsSequentialNumbers()
    {
        var today = DateTime.Now.ToString("MMdd");

        var n1 = SequentialNumberService.GetNextNumber();
        var n2 = SequentialNumberService.GetNextNumber();
        var n3 = SequentialNumberService.GetNextNumber();

        Assert.Equal($"{today}-001", n1);
        Assert.Equal($"{today}-002", n2);
        Assert.Equal($"{today}-003", n3);
    }

    [Fact]
    public void GetNextNumber_DateReset_ResetsToOne()
    {
        var today = DateTime.Now.ToString("MMdd");
        // Write state with yesterday's date prefix
        var state = new CounterState { DatePrefix = "0101", Counter = 42 };
        File.WriteAllText(
            SequentialNumberService.CounterFilePath,
            JsonSerializer.Serialize(state));

        var result = SequentialNumberService.GetNextNumber();

        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void GetNextNumber_Persistence_SavesStateToFile()
    {
        SequentialNumberService.GetNextNumber();

        var json = File.ReadAllText(SequentialNumberService.CounterFilePath);
        var state = JsonSerializer.Deserialize<CounterState>(json);

        Assert.NotNull(state);
        Assert.Equal(DateTime.Now.ToString("MMdd"), state!.DatePrefix);
        Assert.Equal(1, state.Counter);
    }

    [Fact]
    public void GetNextNumber_CorruptFile_RecoverGracefully()
    {
        var today = DateTime.Now.ToString("MMdd");
        // Write corrupt JSON
        File.WriteAllText(SequentialNumberService.CounterFilePath, "{{corrupt json!!");

        var result = SequentialNumberService.GetNextNumber();

        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void GetNextNumber_MissingFile_StartsFromOne()
    {
        var today = DateTime.Now.ToString("MMdd");
        // Ensure file doesn't exist
        if (File.Exists(SequentialNumberService.CounterFilePath))
            File.Delete(SequentialNumberService.CounterFilePath);

        var result = SequentialNumberService.GetNextNumber();

        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void PeekNextNumber_NewDay_ReturnsPreview()
    {
        var today = DateTime.Now.ToString("MMdd");
        // Write state with old date
        var state = new CounterState { DatePrefix = "0101", Counter = 10 };
        File.WriteAllText(
            SequentialNumberService.CounterFilePath,
            JsonSerializer.Serialize(state));

        var result = SequentialNumberService.PeekNextNumber();

        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void PeekNextNumber_Existing_ReturnsLastNumber()
    {
        // Generate 5 numbers
        for (int i = 0; i < 5; i++)
            SequentialNumberService.GetNextNumber();

        var result = SequentialNumberService.PeekNextNumber();
        var today = DateTime.Now.ToString("MMdd");

        Assert.Equal($"{today}-005", result);
    }

    [Fact]
    public async Task GetNextNumber_ConcurrentCalls_NoDuplicates()
    {
        const int threadCount = 10;
        var results = new string[threadCount];
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                results[index] = SequentialNumberService.GetNextNumber();
            });
        }

        await Task.WhenAll(tasks);

        // Q4: Verify uniqueness AND completeness — all numbers 001-010 must be present
        var today = DateTime.Now.ToString("MMdd");
        var uniqueResults = new System.Collections.Generic.HashSet<string>(results);
        Assert.Equal(threadCount, uniqueResults.Count);

        // Verify the expected range of numbers is complete (no gaps)
        for (int i = 1; i <= threadCount; i++)
        {
            Assert.Contains($"{today}-{i:D3}", uniqueResults);
        }
    }

    [Fact]
    public void GetNextNumber_NegativeCounter_RecoverGracefully()
    {
        // Q1/A1: If counter.json has negative counter (manual tamper), should recover
        var today = DateTime.Now.ToString("MMdd");
        var state = new CounterState { DatePrefix = today, Counter = -5 };
        File.WriteAllText(
            SequentialNumberService.CounterFilePath,
            JsonSerializer.Serialize(state));

        var result = SequentialNumberService.GetNextNumber();

        // Negative counter reset to 0, then incremented to 1
        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void GetNextNumber_UnwritablePath_ThrowsException()
    {
        // Q2: SaveState should throw when path is unwritable,
        // ensuring caller (SessionService) knows persistence failed
        SequentialNumberService.CounterFilePath = Path.Combine(
            "/nonexistent_root_path_that_cannot_exist",
            "deeply", "nested", "counter.json");

        Assert.ThrowsAny<Exception>(() => SequentialNumberService.GetNextNumber());
    }

    [Fact]
    public void PeekNextNumber_SameDay_ZeroCounter_ReturnsPreview()
    {
        // Q3: When DatePrefix matches today but Counter is 0 (edge case),
        // should return 001 as preview of the next number to be assigned
        var today = DateTime.Now.ToString("MMdd");
        var state = new CounterState { DatePrefix = today, Counter = 0 };
        File.WriteAllText(
            SequentialNumberService.CounterFilePath,
            JsonSerializer.Serialize(state));

        var result = SequentialNumberService.PeekNextNumber();

        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void GetNextNumber_NullDatePrefix_RecoverGracefully()
    {
        // A1 extension: null DatePrefix in state file should not crash
        var today = DateTime.Now.ToString("MMdd");
        // Manually write JSON with null datePrefix
        File.WriteAllText(
            SequentialNumberService.CounterFilePath,
            "{\"datePrefix\": null, \"counter\": 5}");

        var result = SequentialNumberService.GetNextNumber();

        // null DatePrefix != today → reset to today, counter 0, then increment to 1
        Assert.Equal($"{today}-001", result);
    }
}
