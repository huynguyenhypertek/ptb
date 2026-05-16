using System;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 11: Thank You with QR Code
/// State 1: nen10.5.png — shows QR code for photo download (or offline fallback)
/// State 2: nen11.png — shows "Về MH chính" button  
/// State 3: nen11 xac nhan — confirmation to return
/// </summary>
public partial class ThankYouViewModel : ViewModelBase, IDisposable
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
            var oldBg = BackgroundImage;
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
            oldBg?.Dispose();
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
            // D2: Get sequential number early — fallback to "---" if unavailable
            SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";

            // Check mạng trước khi gọi API
            var isOnline = await NetworkCheckService.IsOnlineAsync();

            if (!isOnline)
            {
                // OFFLINE MODE: hiện thông báo liên hệ nhân viên
                Console.WriteLine($"[QR-ThankYou] Offline mode — seq: {SequentialNumber}");
                IsOffline = true;
                StatusText = "📴 Không có kết nối mạng";
                return;
            }

            if (DeviceConfig.GoogleDriveEnabled)
            {
                var preFetchedUrl = SessionService.CurrentSession.PreFetchedDriveUrl;
                
                if (!string.IsNullOrEmpty(preFetchedUrl))
                {
                    // 🚀 Pre-fetched URL available — generate QR instantly!
                    Console.WriteLine("[QR-ThankYou] Using pre-fetched Drive URL — instant QR!");
                    using var qrGenerator = new QRCoder.QRCodeGenerator();
                    var qrData = qrGenerator.CreateQrCode(preFetchedUrl, QRCoder.QRCodeGenerator.ECCLevel.M);
                    using var qrCode = new QRCoder.PngByteQRCode(qrData);
                    var qrBytes = qrCode.GetGraphic(20, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });
                    
                    using var ms = new System.IO.MemoryStream(qrBytes);
                    var qrBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                    
                    if (_disposed) { qrBitmap.Dispose(); return; }
                    
                    var oldQr = QrCodeImage;
                    QrCodeImage = qrBitmap;
                    oldQr?.Dispose();
                    StatusText = "📱 Quét mã QR để tải ảnh từ Google Drive";
                    Console.WriteLine("[QR-ThankYou] Generated successfully (Google Drive - instant)");
                }
                else
                {
                    // Fallback: pre-fetch didn't complete yet, use retry logic
                    Console.WriteLine("[QR-ThankYou] Pre-fetch not ready, falling back to retry...");
                    var folderName = SessionService.CurrentSession.SessionFolderName;
                    var (qr, status, ok) = await GoogleDriveQRService.WaitSyncAndGenerateQRAsync(
                        folderName ?? "",
                        foregroundColor: new byte[] { 0, 0, 0 },
                        backgroundColor: new byte[] { 255, 255, 255 },
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
                        Console.WriteLine("[QR-ThankYou] Generated successfully (Google Drive)");
                    }
                    else
                    {
                        StatusText = status;
                        Console.WriteLine($"[QR-ThankYou] Google Drive QR failed: {status}");
                    }
                }
            }
            else
            {
                // Giữ nguyên logic QRUploadService hiện tại
                var finalImage = SessionService.CurrentSession.FinalImagePath;
                var (qrBitmap, statusText, success) = await QRUploadService.UploadAndGenerateQRAsync(
                    finalImage,
                    foregroundColor: new byte[] { 0, 0, 0 },
                    backgroundColor: new byte[] { 255, 255, 255 },
                    _cts.Token);

                if (_disposed) { qrBitmap?.Dispose(); return; }

                if (success && qrBitmap != null)
                {
                    var oldQr = QrCodeImage;
                    QrCodeImage = qrBitmap;
                    oldQr?.Dispose();
                    StatusText = statusText;
                    Console.WriteLine("[QR-ThankYou] Generated successfully (ngrok)");
                }
                else
                {
                    StatusText = statusText;
                    Console.WriteLine($"[QR-ThankYou] Upload failed: {statusText}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (!_cts.IsCancellationRequested)
            {
                // Timeout during HTTP request -> activate offline mode
                Console.WriteLine($"[QR-ThankYou] Timeout detected -> Offline mode");
                NetworkCheckService.ResetCache();
                SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";
                IsOffline = true;
                StatusText = "📴 Lỗi kết nối mạng";
            }
            // Expected when disposed during upload
        }
        catch (Exception ex)
        {
            // D4: Network error during API call → also activate offline mode
            // Don't log ex.Message — may contain full AppsScript URL with deployment key
            Console.WriteLine($"[QR-ThankYou] Error: {ex.GetType().Name}");
            // F2: Invalidate cache so downstream callers see offline immediately
            NetworkCheckService.ResetCache();
            SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "---";
            IsOffline = true;
            StatusText = "📴 Lỗi kết nối mạng";
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
            var oldBg = BackgroundImage;
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
            oldBg?.Dispose();
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
            var oldBg = BackgroundImage;
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
            oldBg?.Dispose();
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

    public void Dispose()
    {
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        BackgroundImage?.Dispose();
        BackgroundImage = null;
        QrCodeImage?.Dispose();
        QrCodeImage = null;
    }
}
