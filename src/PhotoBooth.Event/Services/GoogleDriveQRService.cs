using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using QRCoder;

namespace PhotoBooth.Event.Services;

/// <summary>
/// Service tạo QR code từ Google Drive link.
/// Gọi Google Apps Script để lấy link share folder session.
/// </summary>
/// <remarks>
/// Caller owns the returned Bitmap and is responsible for disposing it
/// (matching <see cref="QRUploadService"/> contract).
/// </remarks>
public static class GoogleDriveQRService
{
    private const int MaxRetries = 10;
    private const int RetryDelayMs = 3000;

    /// <summary>
    /// Chờ Google Drive sync xong, lấy link folder, tạo QR code.
    /// </summary>
    /// <returns>
    /// A tuple of (qrBytes, statusText, success).
    /// </returns>
    public static async Task<(byte[]? QrBytes, string StatusText, bool Success)> 
        WaitSyncAndGenerateQRBytesAsync(
            string sessionFolderName,
            byte[] foregroundColor,
            byte[] backgroundColor,
            Action<string>? onStatusUpdate = null,
            CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionFolderName))
            return (null, "❌ Không có thông tin session", false);

        if (string.IsNullOrEmpty(DeviceConfig.AppsScriptUrl))
            return (null, "❌ Chưa cấu hình Apps Script URL (--appsScriptUrl)", false);

        var client = HttpService.Client;
        string? driveUrl = null;

        // Retry loop — chờ Google Drive sync xong
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            
            onStatusUpdate?.Invoke($"⏳ Đang đồng bộ ảnh lên Google Drive... ({attempt}/{MaxRetries})");

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                
                // Build URL with query param — avoid UriBuilder which adds :443 to HTTPS
                // (breaks Google Apps Script redirect handling)
                var separator = DeviceConfig.AppsScriptUrl.Contains('?') ? "&" : "?";
                var url = $"{DeviceConfig.AppsScriptUrl}{separator}folder={Uri.EscapeDataString(sessionFolderName)}";
                
                Console.WriteLine($"[GDRIVE] Attempt {attempt}: calling Apps Script...");
                var json = await client.GetStringAsync(url, timeoutCts.Token);
                var result = JsonSerializer.Deserialize<AppsScriptResponse>(json);

                if (result?.Success == true && !string.IsNullOrEmpty(result.Url))
                {
                    driveUrl = result.Url;
                    break; // Tìm thấy folder, thoát retry loop
                }

                // Folder chưa sync xong, chờ rồi thử lại
                Console.WriteLine($"[GDRIVE] Attempt {attempt}: folder not found yet, retrying in {RetryDelayMs}ms");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) 
            { 
                throw; // F4: Only throw if parent cancellation (ViewModel disposed)
            }
            catch (Exception ex)
            {
                // Handle both HttpRequestException and timeout TaskCanceledException
                Console.WriteLine($"[GDRIVE] Attempt {attempt}: request failed ({ex.GetType().Name}), retrying...");
            }

            if (attempt < MaxRetries)
            {
                await Task.Delay(RetryDelayMs, ct);
            }
        }

        if (string.IsNullOrEmpty(driveUrl))
        {
            return (null, "❌ Không thể đồng bộ ảnh lên Google Drive. Kiểm tra kết nối mạng.", false);
        }

        // Tạo QR code từ Google Drive link
        onStatusUpdate?.Invoke("✅ Đã đồng bộ! Đang tạo mã QR...");

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(driveUrl, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20, foregroundColor, backgroundColor);

        Console.WriteLine("[GDRIVE] QR generated successfully");
        return (qrBytes, "📱 Quét mã QR để tải ảnh từ Google Drive", true);
    }

    /// <summary>
    /// Chờ Google Drive sync xong, lấy link folder, tạo QR code.
    /// </summary>
    /// <returns>
    /// A tuple of (qrBitmap, statusText, success). Caller owns the returned Bitmap
    /// and is responsible for disposing it.
    /// </returns>
    public static async Task<(Bitmap? QrBitmap, string StatusText, bool Success)> 
        WaitSyncAndGenerateQRAsync(
            string sessionFolderName,
            byte[] foregroundColor,
            byte[] backgroundColor,
            Action<string>? onStatusUpdate = null,
            CancellationToken ct = default)
    {
        var (qrBytes, statusText, success) = await WaitSyncAndGenerateQRBytesAsync(
            sessionFolderName, foregroundColor, backgroundColor, onStatusUpdate, ct);

        if (!success || qrBytes == null)
            return (null, statusText, false);

        using var ms = new MemoryStream(qrBytes);
        var qrBitmap = new Bitmap(ms);

        return (qrBitmap, statusText, true);
    }
}

/// <summary>
/// Response từ Google Apps Script.
/// Uses JsonPropertyName for defensive deserialization (matching UploadResult pattern).
/// </summary>
public class AppsScriptResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    [JsonPropertyName("folderName")]
    public string? FolderName { get; set; }
    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }
}
