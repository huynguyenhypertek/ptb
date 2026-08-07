using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Review and print screen ViewModel — shows final composite, prints, navigates back to Start.
/// </summary>
public partial class ReviewPrintViewModel : ViewModelBase, IDisposable
{
    private readonly NavigationService _navigationService;
    private readonly SessionService _sessionService;
    private readonly IPrintService _printService;
    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _idleTimerCts;
    private bool _isPrinting;
    private bool _disposed;

    public int IdleTimeoutMs { get; set; } = 120000; // 2 minutes

    [ObservableProperty]
    private Bitmap? _finalImage;

    [ObservableProperty]
    private int _printCopies = 1;

    [ObservableProperty]
    private string _statusText = "Đang xử lý...";

    [ObservableProperty]
    private int _progress = 0;

    [ObservableProperty]
    private bool _isOffline;

    [ObservableProperty]
    private string _sequentialNumber = "";

    public ReviewPrintViewModel(
        NavigationService navigationService, 
        SessionService sessionService, 
        IPrintService printService)
    {
        _navigationService = navigationService;
        _sessionService = sessionService;
        _printService = printService;

        IsOffline = false;
        SequentialNumber = _sessionService.CurrentSession.SequentialNumber ?? "---";

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (_disposed) return;
            await LoadFinalImageAsync();
            
            await GenerateQROverlayAsync(_cts.Token);
            
            if (!_cts.Token.IsCancellationRequested)
            {
                await StartPrintingCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] InitializeAsync failed: {ex.Message}");
        }
        finally
        {
            if (!_cts.IsCancellationRequested)
            {
                StartIdleTimer();
            }
        }
    }

    private void StartIdleTimer()
    {
        if (_cts.IsCancellationRequested) return;

        _idleTimerCts?.Cancel();
        _idleTimerCts?.Dispose();
        _idleTimerCts = new CancellationTokenSource();
        var token = _idleTimerCts.Token;

        Task.Delay(IdleTimeoutMs, token).ContinueWith(t =>
        {
            if (!t.IsCanceled && !token.IsCancellationRequested)
            {
                Action triggerAction = () =>
                {
                    if (ReturnToStartCommand.CanExecute(null))
                    {
                        System.Diagnostics.Debug.WriteLine("[IDLE] Auto-returning to Start due to inactivity.");
                        ReturnToStartCommand.Execute(null);
                    }
                };

                try
                {
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        triggerAction();
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(triggerAction);
                    }
                }
                catch
                {
                    // Fallback for tests
                    triggerAction();
                }
            }
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    private void IncreaseCopies()
    {
        StartIdleTimer(); // Reset timer on activity
        if (PrintCopies < 10) PrintCopies++;
    }

    [RelayCommand]
    private void DecreaseCopies()
    {
        StartIdleTimer(); // Reset timer on activity
        if (PrintCopies > 1) PrintCopies--;
    }

    [RelayCommand]
    private void ReturnToStart()
    {
        _idleTimerCts?.Cancel();
        _sessionService.StartNewSession();
        _navigationService.NavigateTo<StartViewModel>();

        // Force GC after session ends to reclaim native memory (OpenCV Mat, Avalonia Bitmap)
        // that may not be collected promptly by background GC during continuous 12h operation.
        Task.Run(() =>
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            GC.WaitForPendingFinalizers();
        });
    }

    private async Task LoadFinalImageAsync()
    {
        var path = _sessionService.CurrentSession.FinalImagePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                using var memStream = new MemoryStream();
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    await fileStream.CopyToAsync(memStream);
                }
                memStream.Position = 0;
                var oldFinal = FinalImage;
                FinalImage = new Bitmap(memStream);
                oldFinal?.Dispose();
                System.Diagnostics.Debug.WriteLine($"[DISPLAY] Loaded final image: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load final image: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Final image not found: {path}");
        }
    }

    private void UpdatePrintStatus(string text, int progress = -1)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                StatusText = text;
                if (progress >= 0) Progress = progress;
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusText = text;
                    if (progress >= 0) Progress = progress;
                });
            }
        }
        catch
        {
            StatusText = text;
            if (progress >= 0) Progress = progress;
        }
    }

    [RelayCommand]
    private async Task StartPrintingAsync()
    {
        if (_isPrinting || _disposed) return;
        _isPrinting = true;

        _idleTimerCts?.Cancel(); // Stop timer during printing

        try
        {
            UpdatePrintStatus("Đang gửi lệnh in...", 50);
            
            if (_sessionService.CurrentSession.FinalImagePath != null)
            {
                var success = await _printService.PrintImageAsync(
                    _sessionService.CurrentSession.FinalImagePath,
                    DeviceConfig.PrinterName, 
                    PrintCopies, 
                    _cts.Token);
                
                if (success)
                {
                    UpdatePrintStatus("Đã gửi lệnh in", 100);
                }
                else
                {
                    UpdatePrintStatus("Lỗi máy in");
                }
            }
            else
            {
                UpdatePrintStatus("Không tìm thấy ảnh", 0);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed before printing completes
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] StartPrintingAsync failed: {ex.Message}");
            UpdatePrintStatus("⚠️ Lỗi máy in");
        }
        finally
        {
            _isPrinting = false;
            if (!_cts.IsCancellationRequested)
            {
                StartIdleTimer(); // Restart timer after printing
            }
        }
    }

    private async Task GenerateQROverlayAsync(CancellationToken ct)
    {
        try
        {
            byte[]? qrBytes = null;
            string? overlayStatus = null;

            // Check mạng trước khi gọi API
            var isOnline = await NetworkCheckService.IsOnlineAsync();

            if (!isOnline)
            {
                // OFFLINE MODE: hiện thông báo liên hệ nhân viên
                System.Diagnostics.Debug.WriteLine($"[QR-ReviewPrint] Offline mode — seq: {SequentialNumber}");
                IsOffline = true;
                StatusText = "Không có kết nối mạng";
                return;
            }

            if (DeviceConfig.GoogleDriveEnabled)
            {
                var preFetchedUrl = _sessionService.CurrentSession.PreFetchedDriveUrl;
                
                if (!string.IsNullOrEmpty(preFetchedUrl))
                {
                    // 🚀 Pre-fetched URL available — generate QR instantly!
                    System.Diagnostics.Debug.WriteLine("[QR-ReviewPrint] Using pre-fetched Drive URL — instant QR!");
                    using var qrGenerator = new QRCoder.QRCodeGenerator();
                    using var qrData = qrGenerator.CreateQrCode(preFetchedUrl, QRCoder.QRCodeGenerator.ECCLevel.M);
                    using var qrCode = new QRCoder.PngByteQRCode(qrData);
                    qrBytes = qrCode.GetGraphic(20, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });
                    overlayStatus = "Quét mã QR để tải ảnh từ Google Drive";
                    System.Diagnostics.Debug.WriteLine("[QR-ReviewPrint] Generated successfully (Google Drive - instant)");
                }
                else
                {
                    // Fallback: pre-fetch didn't complete yet, use retry logic
                    System.Diagnostics.Debug.WriteLine("[QR-ReviewPrint] Pre-fetch not ready, falling back to retry...");
                    var folderName = _sessionService.CurrentSession.SessionFolderName;
                    var (bytes, status, ok) = await GoogleDriveQRService.WaitSyncAndGenerateQRBytesAsync(
                        folderName ?? "",
                        foregroundColor: new byte[] { 0, 0, 0 },
                        backgroundColor: new byte[] { 255, 255, 255 },
                        onStatusUpdate: msg => StatusText = msg,
                        ct: ct
                    );

                    if (ok && bytes != null)
                    {
                        qrBytes = bytes;
                        overlayStatus = status;
                        System.Diagnostics.Debug.WriteLine("[QR-ReviewPrint] Generated successfully (Google Drive)");
                    }
                    else
                    {
                        StatusText = status;
                        System.Diagnostics.Debug.WriteLine($"[QR-ReviewPrint] Google Drive QR failed: {status}");
                    }
                }
            }
            else
            {
                // Giữ nguyên logic QRUploadService hiện tại
                var finalImage = _sessionService.CurrentSession.FinalImagePath;
                var (bytes, status, ok) = await QRUploadService.UploadAndGenerateQRBytesAsync(
                    finalImage,
                    foregroundColor: new byte[] { 0, 0, 0 },
                    backgroundColor: new byte[] { 255, 255, 255 },
                    ct);

                if (ok && bytes != null)
                {
                    qrBytes = bytes;
                    overlayStatus = status;
                    System.Diagnostics.Debug.WriteLine("[QR-ReviewPrint] Generated successfully (ngrok)");
                }
                else
                {
                    StatusText = status;
                    System.Diagnostics.Debug.WriteLine($"[QR-ReviewPrint] Upload failed: {status}");
                }
            }

            if (qrBytes != null)
            {
                var finalImagePath = _sessionService.CurrentSession.FinalImagePath;
                if (!string.IsNullOrEmpty(finalImagePath))
                {
                    var success = await Task.Run(() => PhotoBooth.Infrastructure.Services.ImageCompositeService.OverlayQrCode(finalImagePath, qrBytes, DeviceConfig.QrCodeSizePercent), ct);
                    if (success)
                    {
                        StatusText = overlayStatus ?? "Đã tạo mã QR";
                        // Reload FinalImage inside the UI
                        await LoadFinalImageAsync();
                    }
                    else
                    {
                        StatusText = "Không thể chèn mã QR vào ảnh";
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (!ct.IsCancellationRequested)
            {
                // Timeout during HTTP request -> activate offline mode
                System.Diagnostics.Debug.WriteLine($"[QR-ReviewPrint] Timeout detected -> Offline mode");
                NetworkCheckService.ResetCache();
                IsOffline = true;
                StatusText = "Lỗi kết nối mạng";
            }
            // Expected when disposed
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR-ReviewPrint] Error: {ex.GetType().Name}");
            NetworkCheckService.ResetCache();
            IsOffline = true;
            StatusText = "Lỗi kết nối mạng";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _idleTimerCts?.Cancel();
        _idleTimerCts?.Dispose();
        _idleTimerCts = null;

        _cts.Cancel();
        _cts.Dispose();

        FinalImage?.Dispose();
        FinalImage = null;

        GC.SuppressFinalize(this);
    }
}
