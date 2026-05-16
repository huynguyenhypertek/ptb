# Story 1: Sequential Number Service — Quản lý mã số thứ tự

**Epic:** Offline Fallback — Mã số ảnh & Xử lý mất mạng  
**Mức độ:** 🟡 Nhẹ  
**Trạng thái:** `done`  
**Phụ thuộc:** Không (Story 2 & 3 phụ thuộc vào Story này)  
**Điều kiện tiên quyết:** Epic Google Drive QR đã hoàn thành

---

## Mô tả

Tạo service quản lý mã số thứ tự cho mỗi session chụp ảnh. Mã số có dạng `MMDD-NNN` (ví dụ: `0516-001`, `0516-042`). Counter được lưu vào file JSON trên disk để persist qua restart, và tự động reset khi sang ngày mới.

## Acceptance Criteria

- [x] Tạo `SequentialNumberService` trong `PhotoBooth.UI/Services/`
- [x] Mã số có format `MMDD-NNN` (ví dụ: `0516-001`)
- [x] Counter persistent — lưu vào file `~/.photobooth/counter.json`
- [x] Counter auto-reset khi sang ngày mới (so sánh `MMDD` phần ngày)
- [x] Thread-safe — dùng `lock` để tránh race condition (single-process scope)
- [x] Xử lý overflow: khi counter > 999, `D3` format tự mở rộng (ví dụ: `0516-1000`) — KHÔNG cần code xử lý đặc biệt, `D3` đã handle
- [x] `Session` model có property `SequentialNumber` (string, ví dụ `"0516-001"`)
- [x] `SessionService.PrepareSessionDirectory()` tự động gán mã số khi tạo session
- [x] Không gán mã số trùng — chỉ gọi `GetNextNumber()` một lần duy nhất per session
- [x] Unit tests cho `SequentialNumberService` (increment, reset, persistence, corruption recovery, concurrent access)
- [x] Build thành công, không lỗi

## Dev Notes

### ⚠️ CRITICAL: Double-Assignment Risk

`PrepareSessionDirectory()` được gọi từ **HAI nơi**:
1. `BackgroundSelectionViewModel.cs` line 230 — primary call
2. `CaptureViewModel.cs` line 93 — fallback khi directory chưa được tạo

**Quan trọng:** Code trong `PrepareSessionDirectory()` đã có guard — nó luôn tạo session mới. Khi thêm `GetNextNumber()`, nó sẽ **chỉ chạy 1 lần** per session vì session directory chỉ được tạo 1 lần. Tuy nhiên, cần đảm bảo `SequentialNumber` chỉ được gán khi **chưa có giá trị** (null check).

### Namespace & Project Structure

- `SequentialNumberService` → `PhotoBooth.UI/Services/SequentialNumberService.cs`
- Namespace: `PhotoBooth.UI.Services` (giống các service khác trong folder)
- `CounterState` DTO: đặt trong cùng file, cùng namespace — pattern nhỏ gọn, chấp nhận được
- `Session.SequentialNumber` → `PhotoBooth.Core/Models/Session.cs` — thêm property nullable string
- **KHÔNG cần thêm `using` trong `SessionService.cs`** — cùng namespace `PhotoBooth.UI.Services`

### Counter Overflow

Format `D3` chỉ đảm bảo **tối thiểu** 3 chữ số (pad zero). Nếu counter > 999, C# `D3` sẽ tự output đủ chữ số (ví dụ: `1000`, `1001`). Không bị lỗi, chỉ dài hơn. **Không cần code xử lý đặc biệt.**

### Giới hạn đã biết

- `lock` chỉ thread-safe trong **1 process**. Nếu chạy nhiều instance PhotoBooth đồng thời trên cùng máy, có thể bị race condition file. Đây là limitation chấp nhận được vì deployment hiện tại là single-instance.
- **Static class** → không inject được `CounterFilePath` cho unit test. Xem Task 1.4 để biết cách xử lý test (dùng `internal` field + `InternalsVisibleTo`).

### File Counter Location

`~/.photobooth/counter.json` = `Path.Combine(Environment.SpecialFolder.UserProfile, ".photobooth", "counter.json")`

---

## Các Tasks

### Task 1.1: Tạo SequentialNumberService

#### Tạo file mới: `src/PhotoBooth.UI/Services/SequentialNumberService.cs`

```csharp
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
            var today = DateTime.Now.ToString("MMdd");

            // Reset counter nếu sang ngày mới
            if (state.DatePrefix != today)
            {
                state.DatePrefix = today;
                state.Counter = 0;
            }

            state.Counter++;
            SaveState(state);

            return $"{state.DatePrefix}-{state.Counter:D3}";
        }
    }

    /// <summary>
    /// Lấy mã số hiện tại (không tăng counter). Dùng để hiển thị.
    /// Lưu ý: Nếu là ngày mới và chưa chụp, trả về số tiếp theo sẽ được gán (preview).
    /// </summary>
    public static string GetCurrentNumber()
    {
        lock (_lock)
        {
            var state = LoadState();
            var today = DateTime.Now.ToString("MMdd");

            if (state.DatePrefix != today)
                return $"{today}-001"; // Preview: số tiếp theo sẽ được gán

            if (state.Counter == 0)
                return $"{today}-001"; // Chưa có session nào hôm nay

            return $"{state.DatePrefix}-{state.Counter:D3}";
        }
    }

    private static CounterState LoadState()
    {
        try
        {
            if (File.Exists(CounterFilePath))
            {
                var json = File.ReadAllText(CounterFilePath);
                return JsonSerializer.Deserialize<CounterState>(json) ?? new CounterState();
            }
        }
        catch (Exception ex)
        {
            // Corrupt/empty file → reset to fresh state (safe recovery)
            Console.WriteLine($"[COUNTER] Failed to load state (will reset): {ex.Message}");
        }
        return new CounterState();
    }

    private static void SaveState(CounterState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(CounterFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CounterFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNTER] Failed to save state: {ex.Message}");
        }
    }
}

public class CounterState
{
    [JsonPropertyName("datePrefix")]
    public string DatePrefix { get; set; } = "";

    [JsonPropertyName("counter")]
    public int Counter { get; set; } = 0;
}
```

**Thay đổi so với bản gốc:**
- `CounterFilePath` đổi từ `private static readonly` → `internal static` (không `readonly`) để test project có thể override path qua `InternalsVisibleTo`.

### Task 1.2: Thêm `SequentialNumber` vào Session model

#### Sửa file: [Session.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.Core/Models/Session.cs)

Thêm property sau `PreFetchedDriveUrl` (line 33), trước `CreatedAt`:

```csharp
/// <summary>
/// Mã số thứ tự của session (ví dụ: "0516-001").
/// Dùng để in lên ảnh và để khách nhận diện khi liên hệ lấy ảnh offline.
/// </summary>
public string? SequentialNumber { get; set; }
```

### Task 1.3: Gán mã số trong `SessionService.PrepareSessionDirectory()`

#### Sửa file: [SessionService.cs](file:///Users/huynguyen/work/projects/ptb/src/PhotoBooth.UI/Services/SessionService.cs)

Thêm vào **giữa** dòng `Console.WriteLine($"[SESSION] Directory prepared early: {sessionFolder}")` (line 67) và block pre-fetch Google Drive (line 69). **KHÔNG cần thêm `using`** — cùng namespace.

```csharp
// Gán mã số thứ tự cho session (chỉ gán nếu chưa có — tránh double-assignment)
if (string.IsNullOrEmpty(CurrentSession.SequentialNumber))
{
    CurrentSession.SequentialNumber = SequentialNumberService.GetNextNumber();
    Console.WriteLine($"[SESSION] Sequential number: {CurrentSession.SequentialNumber}");
}
```

**Vị trí chính xác trong method `PrepareSessionDirectory()`:**

```csharp
public void PrepareSessionDirectory()
{
    var sessionFolder = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..6]}";
    var basePath = GetSessionsBaseDirectory();
    var fullPath = Path.Combine(basePath, sessionFolder);
    Directory.CreateDirectory(fullPath);

    CurrentSession.SessionDirectory = fullPath;
    CurrentSession.SessionFolderName = sessionFolder;

    Console.WriteLine($"[SESSION] Directory prepared early: {sessionFolder}");

    // ===== THÊM ĐOẠN NÀY =====
    // Gán mã số thứ tự cho session (chỉ gán nếu chưa có — tránh double-assignment)
    if (string.IsNullOrEmpty(CurrentSession.SequentialNumber))
    {
        CurrentSession.SequentialNumber = SequentialNumberService.GetNextNumber();
        Console.WriteLine($"[SESSION] Sequential number: {CurrentSession.SequentialNumber}");
    }
    // ===== KẾT THÚC =====

    // Start background pre-fetch of Google Drive URL during payment/capture screens
    if (DeviceConfig.GoogleDriveEnabled && !string.IsNullOrEmpty(DeviceConfig.AppsScriptUrl))
    {
        _ = PreFetchDriveUrlAsync(sessionFolder);
    }
}
```

### Task 1.4: Unit Tests cho SequentialNumberService

#### ⚠️ QUAN TRỌNG: Test project chưa tồn tại

Thư mục `tests/` **chưa có** trong project. Dev agent cần tạo mới:

**Bước 1: Tạo xUnit test project**
```bash
cd src/PhotoBooth.UI
# Thêm InternalsVisibleTo vào PhotoBooth.UI.csproj
```

Thêm vào `src/PhotoBooth.UI/PhotoBooth.UI.csproj` (trong `<PropertyGroup>` hoặc tạo `<ItemGroup>` mới):
```xml
<ItemGroup>
    <InternalsVisibleTo Include="PhotoBooth.Tests" />
</ItemGroup>
```

**Bước 2: Tạo test project**
```bash
mkdir -p tests/PhotoBooth.Tests
cd tests/PhotoBooth.Tests
dotnet new xunit
dotnet add reference ../../src/PhotoBooth.UI/PhotoBooth.UI.csproj
```

**Bước 3: Thêm test project vào solution**
```bash
# Từ root project
dotnet sln add tests/PhotoBooth.Tests/PhotoBooth.Tests.csproj
```

#### Tạo file: `tests/PhotoBooth.Tests/Services/SequentialNumberServiceTests.cs`

**Strategy:** Mỗi test override `CounterFilePath` sang temp directory riêng, cleanup sau mỗi test. Dùng `IDisposable` pattern.

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PhotoBooth.UI.Services;
using Xunit;

namespace PhotoBooth.Tests.Services;

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
    public void GetCurrentNumber_NewDay_ReturnsPreview()
    {
        var today = DateTime.Now.ToString("MMdd");
        // Write state with old date
        var state = new CounterState { DatePrefix = "0101", Counter = 10 };
        File.WriteAllText(
            SequentialNumberService.CounterFilePath,
            JsonSerializer.Serialize(state));

        var result = SequentialNumberService.GetCurrentNumber();

        Assert.Equal($"{today}-001", result);
    }

    [Fact]
    public void GetCurrentNumber_Existing_ReturnsLastNumber()
    {
        // Generate 5 numbers
        for (int i = 0; i < 5; i++)
            SequentialNumberService.GetNextNumber();

        var result = SequentialNumberService.GetCurrentNumber();
        var today = DateTime.Now.ToString("MMdd");

        Assert.Equal($"{today}-005", result);
    }

    [Fact]
    public void GetNextNumber_ConcurrentCalls_NoDuplicates()
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

        Task.WaitAll(tasks);

        // All results should be unique
        var uniqueResults = new System.Collections.Generic.HashSet<string>(results);
        Assert.Equal(threadCount, uniqueResults.Count);
    }
}
```

> **Lưu ý:** Tests dùng `IDisposable` pattern — xUnit tự gọi `Dispose()` sau mỗi test. Mỗi test instance có temp directory riêng → **không share state**. Tuy nhiên, vì `CounterFilePath` là static field, **tests KHÔNG thể chạy parallel** — cần `[Collection("Sequential")]` nếu xUnit default parallel gây vấn đề.

**Nếu gặp lỗi parallel execution**, thêm file `tests/PhotoBooth.Tests/Services/SequentialCollection.cs`:
```csharp
using Xunit;

namespace PhotoBooth.Tests.Services;

[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection { }
```

Và thêm `[Collection("Sequential")]` attribute vào class `SequentialNumberServiceTests`.

---

## Verification

- [ ] Chạy app → chụp ảnh → log hiện `[SESSION] Sequential number: 0516-001`
- [ ] Chụp thêm → log hiện `0516-002`, `0516-003`...
- [ ] Restart app → chụp → tiếp tục từ số tiếp theo (không reset)
- [ ] Sang ngày mới (hoặc sửa system clock) → reset về `MMDD-001`
- [ ] File `~/.photobooth/counter.json` tồn tại với nội dung hợp lệ
- [ ] Xóa hoặc corrupt `counter.json` → app vẫn chạy, reset về 001
- [ ] Unit tests pass
- [ ] Concurrent test pass (no duplicate numbers)
- [ ] Build thành công

## File List

**New files:**
- `src/PhotoBooth.UI/Services/SequentialNumberService.cs`
- `tests/PhotoBooth.Tests/PhotoBooth.Tests.csproj` (xUnit test project — tạo mới)
- `tests/PhotoBooth.Tests/Services/SequentialNumberServiceTests.cs`

**Modified files:**
- `src/PhotoBooth.Core/Models/Session.cs` — thêm `SequentialNumber` property
- `src/PhotoBooth.UI/Services/SessionService.cs` — gọi `GetNextNumber()` trong `PrepareSessionDirectory()` với null-check guard
- `src/PhotoBooth.UI/PhotoBooth.UI.csproj` — thêm `InternalsVisibleTo` cho test project

---

## Dev Agent Record

### Implementation Notes

**All 4 tasks implemented in single session:**

1. **Task 1.1 — SequentialNumberService**: Created static class with `GetNextNumber()` and `GetCurrentNumber()`. Thread-safe via `lock`, persists to `~/.photobooth/counter.json`, auto-resets on new day (MMDD comparison). Corruption recovery returns fresh state. `CounterFilePath` is `internal static` for test override via `InternalsVisibleTo`.

2. **Task 1.2 — Session.SequentialNumber**: Added `string? SequentialNumber` property to `Session` model in `PhotoBooth.Core/Models/Session.cs` after `PreFetchedDriveUrl`.

3. **Task 1.3 — SessionService integration**: Added `GetNextNumber()` call in `PrepareSessionDirectory()` with `string.IsNullOrEmpty()` guard to prevent double-assignment (method is called from 2 places: BackgroundSelectionViewModel and CaptureViewModel).

4. **Task 1.4 — Unit tests**: Created xUnit test project `PhotoBooth.Tests`, added to solution. 8 tests covering: increment, date reset, persistence, corruption recovery, missing file, current number preview, current number existing, and concurrent access (10 threads). All tests use `IDisposable` pattern with temp directories + `[Collection("Sequential")]` to prevent parallel execution conflicts on static field.

**Fix applied**: Converted `Task.WaitAll` → `await Task.WhenAll` in concurrent test to resolve xUnit1031 analyzer warning.

### Debug Log
- Build: ✅ 0 errors, warnings are pre-existing (NU1903 Tmds.DBus.Protocol vulnerability)
- Tests: ✅ 8/8 passed (0.53s)

## Change Log

| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-05-16 | Tạo story ban đầu | Epic Offline Fallback |
| 2026-05-16 | Party-mode validation: thêm Dev Notes, null-check guard, unit test task, edge case docs | Fix 10+ findings từ multi-agent review |
| 2026-05-16 | Party-mode review round 2: fix 8 findings | **A1:** `CounterFilePath` → `internal static` cho testability. **D2:** Thêm đầy đủ hướng dẫn tạo test project (dotnet new xunit, add reference, InternalsVisibleTo). **Q1:** Thêm test setup steps chi tiết. **Q2:** Test strategy dùng temp directory + IDisposable cleanup. **Q3:** Thêm test case `ConcurrentCalls_NoDuplicates`. **Q4:** Test date reset bằng manipulate counter file. **A2:** Clarify AC overflow wording. **Verification:** Thêm concurrent test verification. |
| 2026-05-16 | ✅ Implementation complete — all 4 tasks done, 8/8 tests pass, build succeeds | Dev agent implementation |
| 2026-05-16 | ✅ Code review complete — 7 findings (1 critical, 2 high, 3 medium, 1 low). Fixed F1-F5: SaveState throws on failure + caller try-catch (F1), cached JsonSerializerOptions (F2), atomic file write via temp+rename (F3), CounterState → internal (F4), warning comments on test collection (F5). F6 (GetCurrentNumber semantics) deferred. F7 (unused import) cosmetic only. 8/8 tests pass. | Adversarial code review |
| 2026-05-16 | ✅ Party-mode review round 3 — 8 findings across 4 agents (Architect, Dev, QA, PM). Fixed: **A1** LoadState validates deserialized state (negative counter, null DatePrefix). **Q1** Added test `NegativeCounter_RecoverGracefully`. **Q2** Added test `UnwritablePath_ThrowsException` for SaveState failure propagation. **Q3** Added test `SameDay_ZeroCounter_ReturnsPreview`. **Q4** Strengthened concurrent test to verify completeness (all 001-010 present), not just uniqueness. **A1-ext** Added test `NullDatePrefix_RecoverGracefully`. W1 (GetCurrentNumber semantics) deferred — design smell only. W2 (Console.WriteLine vs logger) — cosmetic. 12/12 tests pass, 0 errors. | Party-mode multi-agent code review |
| 2026-05-16 | ✅ Final Fixes — Fixed W1 (renamed GetCurrentNumber to PeekNextNumber for clearer semantics) and W2 (replaced Console.WriteLine with System.Diagnostics.Debug.WriteLine). All tests pass. | User requested to fix all findings |

