using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PhotoBooth.Core.Interfaces;

namespace PhotoBooth.Infrastructure.Services;

public class PrintService : IPrintService
{
    /// <summary>IPP printer-state = 5 (stopped).</summary>
    private const int StateStopped = 5;

    /// <summary>
    /// Các lý do khiến máy in không in được ngay.
    /// Cố tình KHÔNG chặn "connecting-to-device" vì đó là trạng thái tạm khi máy in
    /// vừa được đánh thức — nếu máy thật sự mất kết nối thì CUPS kèm thêm "offline-report".
    /// </summary>
    private static readonly string[] BlockingReasons =
    [
        "offline",
        "media-empty",
        "media-needed",
        "media-jam",
        "marker-supply-empty",
        "door-open",
        "cover-open",
        "paused",
    ];

    /// <summary>Hậu tố mức độ mà CUPS gắn vào printer-state-reasons.</summary>
    private static readonly string[] ReasonSeverities = ["-report", "-warning", "-error"];

    public async Task<bool> PrintImageAsync(string imagePath, string printerName, int copies, CancellationToken ct, string mediaType = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                Console.WriteLine("[PRINT] PrinterName is empty, using system default printer.");
            }

            if (copies <= 0) copies = 1;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await PrintUnixAsync(imagePath, printerName, copies, ct, mediaType);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await PrintWindowsAsync(imagePath, printerName, copies, ct);
            }
            else
            {
                Console.WriteLine("[ERROR] Unsupported OS for printing.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] PrintService exception: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PrintUnixAsync(string imagePath, string printerName, int copies, CancellationToken ct, string mediaType = "")
    {
        // Pre-flight: `lp` nhận job và trả exit code 0 ngay cả khi máy in offline hoặc
        // hết giấy — job chỉ nằm xếp hàng vô thời hạn. Không kiểm tra trước thì UI sẽ
        // báo "đã gửi lệnh in" cho khách rồi họ ra về mà không có ảnh.
        if (!await PreflightCheckAsync(printerName, mediaType, ct))
            return false;

        var finalImagePath = imagePath;
        var tempPdfPath = string.Empty;

        // Lỗi kinh điển của driver DNP trên macOS: in file JPG/PNG trực tiếp qua CUPS hay bị
        // sai không gian màu (RGB -> BGR) làm ảnh bị xanh hoặc xỉn màu. Convert qua PDF trước 
        // bằng `sips` sẽ giữ nguyên profile màu chuẩn xác.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
            (imagePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
             imagePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
             imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
        {
            tempPdfPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"print_{Guid.NewGuid():N}.pdf");
            var (sipsCode, _, sipsErr) = await RunAsync("sips", $"-s format pdf \"{imagePath}\" --out \"{tempPdfPath}\"", ct);
            if (sipsCode == 0 && System.IO.File.Exists(tempPdfPath))
            {
                finalImagePath = tempPdfPath;
                Console.WriteLine($"[PRINT] Converted image to PDF for accurate color printing on macOS: {tempPdfPath}");
            }
            else
            {
                Console.WriteLine($"[PRINT] WARNING: Failed to convert image to PDF via sips: {sipsErr}");
            }
        }

        var options = "-o fit-to-page";
        if (!string.IsNullOrWhiteSpace(mediaType))
            options += $" -o media={mediaType}";

        var args = string.IsNullOrWhiteSpace(printerName)
            ? $"-n {copies} {options} \"{finalImagePath}\""
            : $"-n {copies} -d \"{printerName}\" {options} \"{finalImagePath}\"";

        var (exitCode, stdout, stderr) = await RunAsync("lp", args, ct);

        // Dọn dẹp file PDF tạm
        if (!string.IsNullOrEmpty(tempPdfPath) && System.IO.File.Exists(tempPdfPath))
        {
            try { System.IO.File.Delete(tempPdfPath); } catch { /* ignore */ }
        }

        if (exitCode != 0)
        {
            Console.WriteLine($"[ERROR] lp print failed (exit {exitCode}): {stderr.Trim()}");
            return false;
        }

        // Job id có dạng "<queue>-<số>" trong mọi ngôn ngữ, nên regex an toàn hơn
        // là tách theo từ (macOS dịch output của lp theo locale hệ thống).
        var jobId = Regex.Match(stdout, @"[A-Za-z0-9_.\-]+-\d+").Value;
        var target = string.IsNullOrWhiteSpace(printerName) ? "default printer" : printerName;
        Console.WriteLine($"[PRINT] Sent {copies} copy/copies to {target}" +
                          $"{(string.IsNullOrEmpty(mediaType) ? "" : $" (media={mediaType})")}" +
                          $"{(string.IsNullOrEmpty(jobId) ? "" : $", job={jobId}")}");
        return true;
    }

    /// <summary>
    /// Kiểm tra máy in trước khi gửi job, dùng <c>lpoptions</c> vì output là key=value
    /// và KHÔNG bị dịch theo locale — <c>lpstat</c> trả tiếng Việt trên macOS tiếng Việt
    /// nên không parse được tin cậy.
    /// </summary>
    /// <returns>False nếu máy in không dùng được ngay lúc này.</returns>
    private async Task<bool> PreflightCheckAsync(string printerName, string mediaType, CancellationToken ct)
    {
        var target = string.IsNullOrWhiteSpace(printerName) ? "" : $"-p \"{printerName}\"";
        var (_, stdout, _) = await RunAsync("lpoptions", target, ct);

        var opts = ParseOptions(stdout);

        // Không có printer-state nghĩa là queue không tồn tại (lpoptions vẫn exit 0).
        if (!opts.TryGetValue("printer-state", out var stateRaw))
        {
            Console.WriteLine(string.IsNullOrWhiteSpace(printerName)
                ? "[PRINT] ERROR: Không có máy in mặc định nào trong hệ thống."
                : $"[PRINT] ERROR: Không tìm thấy máy in '{printerName}' trong CUPS.");
            return false;
        }

        if (opts.TryGetValue("printer-is-accepting-jobs", out var accepting) &&
            accepting.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[PRINT] ERROR: Máy in đang từ chối nhận job (queue bị reject).");
            return false;
        }

        var reasons = opts.GetValueOrDefault("printer-state-reasons", "none");
        var (blocking, warnings) = ClassifyReasons(reasons);

        // Cảnh báo (ribbon sắp hết...) chỉ log, KHÔNG chặn in — chặn vì một cảnh báo
        // sẽ làm dừng in giữa sự kiện dù máy vẫn in được.
        if (warnings.Length > 0)
            Console.WriteLine($"[PRINT] WARNING: {string.Join(", ", warnings)}");

        if (blocking.Length > 0)
        {
            Console.WriteLine($"[PRINT] ERROR: Máy in chưa sẵn sàng — {string.Join(", ", blocking)}");
            return false;
        }

        if (int.TryParse(stateRaw, out var state) && state == StateStopped)
        {
            Console.WriteLine($"[PRINT] ERROR: Máy in đang ở trạng thái stopped (state=5, reasons={reasons})");
            return false;
        }

        // printer-state/reasons chỉ phản ánh trạng thái MÁY IN, không phản ánh trạng thái
        // JOB — CUPS cho phép 1 job cụ thể bị "held"/kẹt trong khi printer-state vẫn báo
        // idle/accepting. Không check riêng thì khách sau xếp hàng vô thời hạn sau job kẹt
        // của khách trước mà preflight vẫn báo OK.
        if (await HasStuckJobAsync(printerName, ct))
        {
            Console.WriteLine("[PRINT] ERROR: Có job in cũ đang kẹt trong queue quá lâu.");
            return false;
        }

        // Khổ giấy sai bị CUPS bỏ qua âm thầm rồi in ra khổ mặc định của driver
        // (DNP mặc định 310dnp6x8, không phải 6x4) — chỉ cảnh báo, không chặn in.
        if (!string.IsNullOrWhiteSpace(mediaType))
            await WarnIfMediaUnsupportedAsync(printerName, mediaType, ct);

        return true;
    }

    /// <summary>Tuổi tối đa (giây) một job được phép chờ trong queue trước khi coi là kẹt.
    /// 1 job đang in bình thường xong trong vài giây — dư sức dưới ngưỡng này.</summary>
    private const int StuckJobAgeThresholdSec = 30;

    /// <summary>
    /// Kiểm tra job cũ nhất trong queue của máy in — dùng <c>lpstat -o</c> (không phải <c>lpq</c>)
    /// vì đây là cách duy nhất có cột timestamp; <c>lpq</c>/<c>lpq -l</c> chỉ in hạng ("active", "1st"...)
    /// mà không có giờ nộp job. Bắt buộc <c>LC_ALL=C</c> vì <c>lpstat</c> dịch cả nhãn cột
    /// (không chỉ giá trị) theo locale hệ thống — ép về locale C để định dạng ngày luôn
    /// là "Thu Aug 6 14:27:45 2026", parse được bằng DateTime.TryParse.
    /// Chỉ chặn khi job cũ nhất đã tồn tại quá <see cref="StuckJobAgeThresholdSec"/> — 1 job
    /// đang in bình thường (vài giây) không bị chặn nhầm.
    /// </summary>
    private static async Task<bool> HasStuckJobAsync(string printerName, CancellationToken ct)
    {
        var target = string.IsNullOrWhiteSpace(printerName) ? "" : $"-d \"{printerName}\"";
        var (exitCode, stdout, _) = await RunAsync("lpstat", $"-o {target}".Trim(), ct, forceInvariantLocale: true);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout)) return false;

        // Mỗi dòng: "<job-id> <owner> <size> <thứ> <tháng> <ngày> <giờ:phút:giây> <năm>"
        // — timestamp luôn là 5 token cuối, tách theo khoảng trắng rồi ghép lại để parse.
        DateTime? oldest = null;
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5) continue;

            var timestampStr = string.Join(' ', tokens[^4..]);
            if (!DateTime.TryParse(timestampStr, out var submitted)) continue;

            if (oldest == null || submitted < oldest) oldest = submitted;
        }

        if (oldest == null) return false;

        var age = DateTime.Now - oldest.Value;
        if (age.TotalSeconds < StuckJobAgeThresholdSec) return false;

        Console.WriteLine($"[PRINT] WARNING: Job cũ nhất trong queue đã chờ {age.TotalSeconds:F0}s (nộp lúc {oldest.Value}) — vượt ngưỡng {StuckJobAgeThresholdSec}s.");
        return true;
    }

    private async Task WarnIfMediaUnsupportedAsync(string printerName, string mediaType, CancellationToken ct)
    {
        var target = string.IsNullOrWhiteSpace(printerName) ? "" : $"-p \"{printerName}\" ";
        var (exitCode, stdout, _) = await RunAsync("lpoptions", $"{target}-l", ct);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout)) return;

        var line = stdout.Split('\n').FirstOrDefault(l => l.StartsWith("PageSize/", StringComparison.Ordinal));
        if (line == null) return;

        // Format: "PageSize/Media Size: 4x6 *A4 Letter" — '*' đánh dấu giá trị đang mặc định
        var values = line[(line.IndexOf(':') + 1)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.TrimStart('*'))
            .ToArray();

        if (values.Length > 0 && !values.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[PRINT] WARNING: Máy in không hỗ trợ khổ '{mediaType}' — " +
                              $"CUPS sẽ bỏ qua và in ra khổ mặc định. Khổ hợp lệ: {string.Join(", ", values)}");
        }
    }

    /// <summary>
    /// Phân loại printer-state-reasons theo hậu tố mức độ. Mỗi lý do có dạng "tên" hoặc
    /// "tên-report" / "tên-warning" / "tên-error".
    /// Chỉ "-warning" là advisory: "media-empty-warning" nghĩa là ribbon sắp hết nhưng vẫn
    /// in được, còn "media-empty-error" là đã hết thật.
    /// Phải tách hậu tố rồi so khớp CHÍNH XÁC — nếu dùng Contains thì
    /// "media-empty-warning" sẽ khớp "media-empty" và chặn in oan giữa sự kiện.
    /// "offline-report" tuy mang hậu tố report nhưng vẫn chặn, vì đó là giá trị macOS
    /// dùng khi máy in mất kết nối (đúng trạng thái làm job xếp hàng vô thời hạn).
    /// </summary>
    private static (string[] Blocking, string[] Warnings) ClassifyReasons(string reasons)
    {
        var blocking = new List<string>();
        var warnings = new List<string>();

        foreach (var raw in reasons.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var reason = raw.Trim();
            if (reason.Length == 0 || reason.Equals("none", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = reason;
            var severity = "";
            foreach (var suffix in ReasonSeverities)
            {
                if (reason.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    severity = suffix;
                    name = reason[..^suffix.Length];
                    break;
                }
            }

            if (!BlockingReasons.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            if (severity.Equals("-warning", StringComparison.OrdinalIgnoreCase))
                warnings.Add(reason);
            else
                blocking.Add(reason);
        }

        return (blocking.ToArray(), warnings.ToArray());
    }

    private static Dictionary<string, string> ParseOptions(string stdout)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in stdout.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = token.IndexOf('=');
            if (eq > 0) result[token[..eq]] = token[(eq + 1)..].Trim();
        }
        return result;
    }

    /// <summary>Timeout nội bộ cho mỗi lời gọi process in — CUPS/lp bị wedge sẽ bị kill
    /// thay vì treo UI vô thời hạn (không có timeout này, caller chỉ có CancellationToken
    /// của session, mà token đó chỉ cancel khi Dispose ViewModel).</summary>
    private const int ProcessTimeoutMs = 15000;

    /// <summary>Chạy process, đọc stdout/stderr song song để tránh nghẽn pipe buffer.
    /// Kill process nếu vượt ProcessTimeoutMs hoặc caller cancel — không để process
    /// orphan chạy tiếp sau khi await đã bỏ qua.</summary>
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName, string arguments, CancellationToken ct, bool forceInvariantLocale = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // lpstat dịch cả nhãn cột theo locale hệ thống (không chỉ giá trị) — ép LC_ALL=C
        // để định dạng timestamp cố định, parse được bằng DateTime.TryParse.
        if (forceInvariantLocale)
            startInfo.Environment["LC_ALL"] = "C";

        using var process = Process.Start(startInfo);
        if (process == null) return (-1, "", $"Không khởi động được '{fileName}'");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ProcessTimeoutMs);

        // Phải đọc trước khi WaitForExit: nếu chờ exit trước rồi mới đọc, process
        // có thể treo khi output vượt kích thước pipe buffer của OS.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout nội bộ (không phải caller cancel) — process bị wedge, kill để
            // không orphan chạy mãi và không để UI chờ vô thời hạn.
            Console.WriteLine($"[PRINT] ERROR: '{fileName}' timed out after {ProcessTimeoutMs}ms — killing process");
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            return (-1, "", $"Process '{fileName}' timed out after {ProcessTimeoutMs}ms");
        }
        catch (OperationCanceledException)
        {
            // Caller cancel thật (Dispose) — vẫn kill process để tránh orphan.
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            throw;
        }

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private async Task<bool> PrintWindowsAsync(string imagePath, string printerName, int copies, CancellationToken ct)
    {
        // On Windows, mspaint /p doesn't easily accept printer name or copies without setting default printer.
        // We will use PowerShell Start-Process to send to the specific printer.
        // Wait, mspaint /pt "filename" "printername" works for specific printers.
        // To handle copies, we loop.

        for (int i = 0; i < copies; i++)
        {
            ct.ThrowIfCancellationRequested();

            var startInfo = new ProcessStartInfo
            {
                FileName = "mspaint.exe",
                Arguments = $"/pt \"{imagePath}\" \"{printerName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            // Paint usually spawns and delegates to spooler, then exits.
            await process.WaitForExitAsync(ct);
            
            // Add a small delay between copies to avoid spooler congestion
            if (i < copies - 1)
            {
                await Task.Delay(500, ct);
            }
        }

        Console.WriteLine($"[PRINT] Successfully sent {copies} copies to {printerName} via mspaint");
        return true;
    }
}
