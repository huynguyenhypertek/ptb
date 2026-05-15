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
            if (DeviceConfig.GoogleDriveEnabled)
            {
                var folderName = SessionService.CurrentSession.SessionFolderName;
                var (qr, status, ok) = await GoogleDriveQRService.WaitSyncAndGenerateQRAsync(
                    folderName ?? "",
                    foregroundColor: new byte[] { 255, 255, 255 },
                    backgroundColor: new byte[] { 30, 30, 46 },
                    onStatusUpdate: msg => StatusText = msg,
                    ct: _cts.Token
                );

                if (_disposed) { qr?.Dispose(); return; }

                if (ok && qr != null)
                {
                    var oldQr = QrCodeImage;
                    QrCodeImage = qr;
                    oldQr?.Dispose();
                    StatusText = status;
                    Console.WriteLine("[QR] Generated successfully (Google Drive)");
                }
                else
                {
                    StatusText = status;
                    Console.WriteLine($"[QR] Google Drive QR failed: {status}");
                }
            }
            else
            {
                // Giữ nguyên logic QRUploadService hiện tại
                var finalImage = SessionService.CurrentSession.FinalImagePath;
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
                    Console.WriteLine("[QR] Generated successfully (ngrok)");
                }
                else
                {
                    StatusText = statusText;
                    Console.WriteLine($"[QR] Upload failed: {statusText}");
                }
            }

            IsUploading = false;
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed during upload
            IsUploading = false;
        }
        catch (Exception ex)
        {
            // Don't log ex.Message — may contain full AppsScript URL with deployment key
            Console.WriteLine($"[QR] Error: {ex.GetType().Name}");
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
