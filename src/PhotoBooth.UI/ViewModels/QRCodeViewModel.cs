using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;
using QRCoder;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 12: QR Code to download photos on phone
/// </summary>
public partial class QRCodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _qrCodeImage;

    [ObservableProperty]
    private string _statusText = "⏳ Đang tải ảnh lên...";

    [ObservableProperty]
    private bool _isUploading = true;

    [ObservableProperty]
    private bool _showReturnPopup;

    public QRCodeViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        _ = UploadAndGenerateQR();
    }

    private async System.Threading.Tasks.Task UploadAndGenerateQR()
    {
        try
        {
            var finalImage = SessionService.CurrentSession.FinalImagePath;
            if (string.IsNullOrEmpty(finalImage) || !File.Exists(finalImage))
            {
                StatusText = "❌ Không tìm thấy ảnh";
                IsUploading = false;
                return;
            }

            // Upload to API
            var apiBase = DeviceConfig.ApiBaseUrl;
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "1");
            client.Timeout = TimeSpan.FromSeconds(30);

            using var form = new MultipartFormDataContent();
            var fileBytes = await File.ReadAllBytesAsync(finalImage);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "file", "photo.png");

            var response = await client.PostAsync($"{apiBase}/api/photos/upload", form);
            
            if (!response.IsSuccessStatusCode)
            {
                StatusText = "❌ Upload thất bại";
                IsUploading = false;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UploadResult>(json);
            
            if (result?.code == null)
            {
                StatusText = "❌ Lỗi xử lý";
                IsUploading = false;
                return;
            }

            // Generate QR code pointing to download page
            var downloadUrl = $"{apiBase}/photos/{result.code}";
            Console.WriteLine($"[QR] Download URL: {downloadUrl}");

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(downloadUrl, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(20, new byte[] { 255, 255, 255 }, new byte[] { 30, 30, 46 });

            using var ms = new MemoryStream(qrBytes);
            QrCodeImage = new Bitmap(ms);

            StatusText = "📱 Quét mã QR để tải ảnh về điện thoại";
            IsUploading = false;

            Console.WriteLine($"[QR] Generated for: {downloadUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QR] Error: {ex.Message}");
            StatusText = "❌ Lỗi kết nối. Thử lại sau.";
            IsUploading = false;
        }
    }

    [RelayCommand]
    private void ShowReturn()
    {
        ShowReturnPopup = true;
    }

    [RelayCommand]
    private void ConfirmReturn()
    {
        ShowReturnPopup = false;
        NavigationService.NavigateTo<StartViewModel>();
    }

    [RelayCommand]
    private void CancelReturn()
    {
        ShowReturnPopup = false;
    }
}


