using System;
using System.IO;

namespace PhotoBooth.Event;

/// <summary>
/// Stores device info passed from Admin app login (via command-line args).
/// Simplified for Event flow — no layout pricing (PriceLayout6/PriceLayout2).
/// </summary>
public static class DeviceConfig
{
    public static int? StoreId { get; set; }
    public static string DeviceId { get; set; } = "device-1";
    public static string PlanType { get; set; } = "Pro";  // "Basic" or "Pro"
    private static string _apiBaseUrl = "http://localhost:5148";
    public static string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => _apiBaseUrl = value?.TrimEnd('/') ?? "";
    }

    public static bool EnablePrinting { get; set; } = false;
    public static string PrinterName { get; set; } = "";
    public static string PrintMedia { get; set; } = ""; // Khổ giấy, VD: "300dnp6x4" cho DNP 10x15cm
    public static int CountdownSeconds { get; set; } = 3;
    public static string EventName { get; set; } = "DONGFEST";
    public static int QrCodeSizePercent { get; set; } = 10; // % of image height (1-30)

    // Google Drive configuration
    public static bool GoogleDriveEnabled { get; set; } = true;
    public static string GoogleDrivePath { get; set; } = "";
    public static string AppsScriptUrl { get; set; } = "";

    /// <summary>
    /// Returns GoogleDrivePath with PII masked — shows only last 2 path segments.
    /// Use this in all log output to avoid leaking user email from Google Drive paths.
    /// </summary>
    public static string GetMaskedGDrivePath()
    {
        if (string.IsNullOrEmpty(GoogleDrivePath)) return "";
        var segments = GoogleDrivePath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (segments.Length < 2) return ".../";
        return ".../" + string.Join("/", segments[^2..]);
    }

    /// <summary>
    /// Returns AppsScriptUrl with deployment key masked — shows only domain + first 10 chars.
    /// Deployment keys (AKfycb...) are secrets that should not appear in logs.
    /// </summary>
    public static string GetMaskedAppsScriptUrl()
    {
        if (string.IsNullOrEmpty(AppsScriptUrl)) return "";
        try
        {
            var uri = new Uri(AppsScriptUrl);
            var path = uri.AbsolutePath;
            var masked = path.Length > 10 ? path[..10] + "..." : path;
            return $"{uri.Scheme}://{uri.Host}{masked}";
        }
        catch (Exception)
        {
            return "<invalid-url>";
        }
    }

    public static void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--storeId="))
            {
                if (int.TryParse(arg.Substring("--storeId=".Length), out var storeId))
                    StoreId = storeId;
                else
                    Console.WriteLine($"[CONFIG] WARNING: Invalid --storeId value: {arg.Substring("--storeId=".Length)}");
            }
            else if (arg.StartsWith("--deviceId="))
            {
                var val = arg.Substring("--deviceId=".Length);
                if (!string.IsNullOrWhiteSpace(val))
                    DeviceId = val;
            }
            else if (arg.StartsWith("--planType="))
            {
                var val = arg.Substring("--planType=".Length);
                if (!string.IsNullOrWhiteSpace(val))
                    PlanType = val;
            }
            else if (arg.StartsWith("--apiBaseUrl="))
            {
                var val = arg.Substring("--apiBaseUrl=".Length);
                if (!string.IsNullOrWhiteSpace(val))
                    ApiBaseUrl = val;
            }
            // NOTE: --priceLayout6= and --priceLayout2= intentionally NOT parsed
            // Event flow has no payment — layout pricing is irrelevant
            else if (arg.StartsWith("--googleDriveEnabled="))
            {
                GoogleDriveEnabled = bool.TryParse(arg.Substring("--googleDriveEnabled=".Length), out var gde) && gde;
            }
            else if (arg.StartsWith("--googleDrivePath="))
            {
                var path = arg.Substring("--googleDrivePath=".Length);
                if (!string.IsNullOrWhiteSpace(path))
                    GoogleDrivePath = path;
            }
            else if (arg.StartsWith("--appsScriptUrl="))
            {
                var url = arg.Substring("--appsScriptUrl=".Length);
                if (!string.IsNullOrWhiteSpace(url))
                    AppsScriptUrl = url;
            }
            else if (arg.StartsWith("--enablePrinting="))
            {
                EnablePrinting = bool.TryParse(arg.Substring("--enablePrinting=".Length), out var ep) && ep;
            }
            else if (arg.StartsWith("--printerName="))
            {
                var name = arg.Substring("--printerName=".Length);
                if (!string.IsNullOrWhiteSpace(name))
                    PrinterName = name;
            }
            else if (arg.StartsWith("--printMedia="))
            {
                var media = arg.Substring("--printMedia=".Length);
                if (!string.IsNullOrWhiteSpace(media))
                    PrintMedia = media;
            }
            else if (arg.StartsWith("--countdownSeconds="))
            {
                if (int.TryParse(arg.Substring("--countdownSeconds=".Length), out var cs))
                    CountdownSeconds = cs;
            }
            else if (arg.StartsWith("--eventName="))
            {
                var name = arg.Substring("--eventName=".Length);
                if (!string.IsNullOrWhiteSpace(name))
                    EventName = name;
            }
            else if (arg.StartsWith("--qrCodeSizePercent="))
            {
                if (int.TryParse(arg.Substring("--qrCodeSizePercent=".Length), out var qr) && qr >= 1 && qr <= 30)
                    QrCodeSizePercent = qr;
            }
            else if (arg.StartsWith("--"))
            {
                Console.WriteLine($"[CONFIG] WARNING: Unrecognized argument: {arg}");
            }
        }

        Console.WriteLine(
            $"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, " +
            $"ApiBaseUrl={ApiBaseUrl}, GoogleDriveEnabled={GoogleDriveEnabled}, " +
            $"GoogleDrivePath={GetMaskedGDrivePath()}, AppsScriptUrl={GetMaskedAppsScriptUrl()}, " +
            $"EnablePrinting={EnablePrinting}, PrinterName={PrinterName}, PrintMedia={PrintMedia}, CountdownSeconds={CountdownSeconds}, EventName={EventName}, QrCodeSizePercent={QrCodeSizePercent}");
    }
}
