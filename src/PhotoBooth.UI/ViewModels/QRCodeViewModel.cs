using System;
using System.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 12: QR Code to download photos on phone
/// </summary>
public partial class QRCodeViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private Bitmap? _qrCodeImage;

    [ObservableProperty]
    private string _statusText = "⏳ Đang tải ảnh lên...";

    [ObservableProperty]
    private bool _isUploading = true;

    [ObservableProperty]
    private bool _showReturnPopup;

    private bool _disposed;
    private readonly CancellationTokenSource _cts = new();

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

            // White QR on dark background (matching original QRCodeViewModel style)
            var (qrBitmap, statusText, success) = await QRUploadService.UploadAndGenerateQRAsync(
                finalImage,
                foregroundColor: new byte[] { 255, 255, 255 },
                backgroundColor: new byte[] { 30, 30, 46 },
                _cts.Token);

            if (_disposed) { qrBitmap?.Dispose(); return; }

            if (success && qrBitmap != null)
            {
                var oldQr = QrCodeImage;
                QrCodeImage = qrBitmap;
                oldQr?.Dispose();
                StatusText = statusText;
            }
            else
            {
                StatusText = statusText;
            }

            IsUploading = false;
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed during upload
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
        // Finding 5: Trigger session cleanup before returning to Start
        SessionService.StartNewSession();
        NavigationService.NavigateTo<StartViewModel>();
    }

    [RelayCommand]
    private void CancelReturn()
    {
        ShowReturnPopup = false;
    }

    public void Dispose()
    {
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        QrCodeImage?.Dispose();
        QrCodeImage = null;
    }
}
