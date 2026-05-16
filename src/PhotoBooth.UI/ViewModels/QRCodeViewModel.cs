using System;
using System.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 12: QR Code to download photos on phone
/// Supports offline fallback: shows sequential number + contact message when network is down.
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

    /// <summary>
    /// True when the device is offline — shows offline fallback UI with sequential number.
    /// </summary>
    [ObservableProperty]
    private bool _isOffline;

    /// <summary>
    /// D2: Shows sequential number or fallback "---" when unavailable.
    /// </summary>
    [ObservableProperty]
    private string _sequentialNumber = "";

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
            // D2: Get sequential number early — fallback to "---" if unavailable
            SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";

            // Check mạng trước khi gọi API
            var isOnline = await NetworkCheckService.IsOnlineAsync();

            if (!isOnline)
            {
                // OFFLINE MODE: hiện thông báo liên hệ nhân viên
                Console.WriteLine($"[QR] Offline mode — seq: {SequentialNumber}");
                IsOffline = true;
                IsUploading = false;
                StatusText = "📴 Không có kết nối mạng";
                return;
            }

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
            if (!_cts.IsCancellationRequested)
            {
                // Timeout during HTTP request -> activate offline mode
                Console.WriteLine($"[QR] Timeout detected -> Offline mode");
                NetworkCheckService.ResetCache();
                SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";
                IsOffline = true;
                StatusText = "📴 Lỗi kết nối mạng";
            }
            // Expected when disposed during upload
            IsUploading = false;
        }
        catch (Exception ex)
        {
            // D4: Network error during API call → also activate offline mode
            // Don't log ex.Message — may contain full AppsScript URL with deployment key
            Console.WriteLine($"[QR] Error: {ex.GetType().Name}");
            // F2: Invalidate cache so downstream callers see offline immediately
            NetworkCheckService.ResetCache();
            SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";
            IsOffline = true;
            StatusText = "📴 Lỗi kết nối mạng";
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
