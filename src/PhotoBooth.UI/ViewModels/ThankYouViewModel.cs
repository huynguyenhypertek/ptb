using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;
using QRCoder;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 11: Thank You with QR Code
/// State 1: nen10.5.png — shows QR code for photo download
/// State 2: nen11.png — shows "Về MH chính" button  
/// State 3: nen11 xac nhan — confirmation to return
/// </summary>
public partial class ThankYouViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _backgroundImage;

    [ObservableProperty]
    private Bitmap? _qrCodeImage;

    [ObservableProperty]
    private string _statusText = "⏳ Đang tải ảnh lên...";

    [ObservableProperty]
    private bool _showQRScreen = true;  // State 1: QR

    [ObservableProperty]
    private bool _showThankYou = false; // State 2: Thank you

    [ObservableProperty]
    private bool _showConfirmation = false; // State 3: Confirm return

    public ThankYouViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadQRBackground();
        _ = UploadAndGenerateQR();
    }

    private void LoadQRBackground()
    {
        try
        {
            var bgPath = "avares://PhotoBooth.UI/Assets/backgrounds/nen10.5.png";
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load nen10.5: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task UploadAndGenerateQR()
    {
        try
        {
            var finalImage = SessionService.CurrentSession.FinalImagePath;
            if (string.IsNullOrEmpty(finalImage) || !File.Exists(finalImage))
            {
                StatusText = "❌ Không tìm thấy ảnh";
                return;
            }

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
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UploadResult>(json);

            if (result?.code == null)
            {
                StatusText = "❌ Lỗi xử lý";
                return;
            }

            // Generate QR code
            var downloadUrl = $"{apiBase}/photos/{result.code}";
            Console.WriteLine($"[QR] Download URL: {downloadUrl}");

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(downloadUrl, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(20, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });

            using var ms = new MemoryStream(qrBytes);
            QrCodeImage = new Bitmap(ms);

            StatusText = "📱 Quét mã QR để tải ảnh";
            Console.WriteLine($"[QR] Generated successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QR] Error: {ex.Message}");
            StatusText = "❌ Lỗi kết nối";
        }
    }

    /// <summary>
    /// From QR screen → Thank you screen
    /// </summary>
    [RelayCommand]
    private void GoToThankYou()
    {
        try
        {
            var bgPath = "avares://PhotoBooth.UI/Assets/backgrounds/nen11.png";
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load nen11: {ex.Message}");
        }
        ShowQRScreen = false;
        ShowThankYou = true;
        ShowConfirmation = false;
    }

    /// <summary>
    /// From Thank you → Confirmation
    /// </summary>
    [RelayCommand]
    private void ShowConfirmReturn()
    {
        try
        {
            var bgPath = "avares://PhotoBooth.UI/Assets/backgrounds/nen11 xac nhan quay ve man hinh chinh.png";
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
            ShowQRScreen = false;
            ShowThankYou = false;
            ShowConfirmation = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load confirmation bg: {ex.Message}");
        }
    }

    /// <summary>
    /// Return to start screen
    /// </summary>
    [RelayCommand]
    private void ReturnToStart()
    {
        SessionService.StartNewSession();
        NavigationService.NavigateTo<StartViewModel>();
    }
}

public class UploadResult
{
    public string? code { get; set; }
    public string? fileName { get; set; }
}
