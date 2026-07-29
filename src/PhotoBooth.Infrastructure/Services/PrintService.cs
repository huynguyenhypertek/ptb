using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PhotoBooth.Core.Interfaces;

namespace PhotoBooth.Infrastructure.Services;

public class PrintService : IPrintService
{
    public async Task<bool> PrintImageAsync(string imagePath, string printerName, int copies, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                Console.WriteLine("[ERROR] PrinterName is not configured.");
                return false;
            }

            if (copies <= 0) copies = 1;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await PrintUnixAsync(imagePath, printerName, copies, ct);
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

    private async Task<bool> PrintUnixAsync(string imagePath, string printerName, int copies, CancellationToken ct)
    {
        // lp -n 3 -d "DNP_DS_RX1HS" "/path/to/image.jpg"
        var args = $"-n {copies} -d \"{printerName}\" \"{imagePath}\"";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "lp",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process == null) return false;

        await process.WaitForExitAsync(ct);
        
        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync(ct);
            Console.WriteLine($"[ERROR] lp print failed: {err}");
            return false;
        }

        Console.WriteLine($"[PRINT] Successfully sent {copies} copies to {printerName} via lp");
        return true;
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
