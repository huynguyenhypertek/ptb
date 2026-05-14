using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using QRCoder;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Shared service for uploading a photo to the API and generating a QR code
/// pointing to the download page. Used by both QRCodeViewModel and ThankYouViewModel.
/// </summary>
public static class QRUploadService
{
    /// <summary>
    /// Uploads the image at <paramref name="imagePath"/> to the server and generates
    /// a QR code bitmap pointing to the download URL.
    /// </summary>
    /// <returns>
    /// A tuple of (qrBitmap, statusText, success). Caller owns the returned Bitmap
    /// and is responsible for disposing it.
    /// </returns>
    public static async Task<(Bitmap? QrBitmap, string StatusText, bool Success)> UploadAndGenerateQRAsync(
        string? imagePath,
        byte[] foregroundColor,
        byte[] backgroundColor,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            return (null, "❌ Không tìm thấy ảnh", false);
        }

        var apiBase = DeviceConfig.ApiBaseUrl;
        var client = HttpService.Client;

        using var form = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(imagePath, ct);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "photo.png");

        // Finding 3: Per-call timeout to prevent indefinite UI hang on slow server
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
        // Finding 1: Dispose HttpResponseMessage to prevent connection/stream leak
        using var response = await client.PostAsync($"{apiBase}/api/photos/upload", form, timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
        {
            return (null, "❌ Upload thất bại", false);
        }

        var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        var result = JsonSerializer.Deserialize<UploadResult>(json);

        if (result?.code == null)
        {
            return (null, "❌ Lỗi xử lý", false);
        }

        // Generate QR code pointing to download page
        var downloadUrl = $"{apiBase}/photos/{result.code}";
        Console.WriteLine($"[QR] Download URL: {downloadUrl}");

        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(downloadUrl, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20, foregroundColor, backgroundColor);

        using var ms = new MemoryStream(qrBytes);
        var qrBitmap = new Bitmap(ms);

        Console.WriteLine($"[QR] Generated successfully for: {downloadUrl}");
        return (qrBitmap, "📱 Quét mã QR để tải ảnh", true);
    }
}

/// <summary>
/// Represents the JSON response from the photo upload API.
/// </summary>
public class UploadResult
{
    [JsonPropertyName("code")]
    public string? code { get; set; }
    [JsonPropertyName("fileName")]
    public string? fileName { get; set; }
}
