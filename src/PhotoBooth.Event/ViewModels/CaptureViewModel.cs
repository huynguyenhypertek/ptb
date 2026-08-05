using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Core.Models;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.Event.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Screen 2: Photo Capture with camera preview and countdown.
/// Captures 6 photos with hardcoded layout6 crop dimensions.
/// Simplified from PhotoBooth.UI — no layout selection, no reconnect logic.
/// </summary>
public partial class CaptureViewModel : ViewModelBase, IDisposable
{
    private readonly NavigationService _navigationService;
    private readonly SessionService _sessionService;
    private readonly ICameraService _cameraService;
    private readonly string _photosDirectory;

    // Timing constants
    private const int CountdownTickMs = 1000;
    private const int InterPhotoDelayMs = 1000;
    private const int TransitionDelayMs = 1000;
    private const int FlashDurationMs = 200;

    [ObservableProperty]
    private int _currentPhotoIndex;

    [ObservableProperty]
    private int _totalPhotos = 8;

    [ObservableProperty]
    private int _countdown;

    [ObservableProperty]
    private bool _isCountingDown;

    [ObservableProperty]
    private bool _isCameraReady;

    [ObservableProperty]
    private bool _isShootingInProgress;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private Bitmap? _cameraPreview;

    [ObservableProperty]
    private ObservableCollection<string> _capturedPhotos = new();

    [ObservableProperty]
    private string _statusMessage = "Connecting camera...";

    [ObservableProperty]
    private int _countdownDuration = DeviceConfig.CountdownSeconds;

    private bool _disposed;
    private volatile bool _isRenderingFrame;
    private long _droppedFrameCount;
    private volatile bool _eventSubscribed;

    // Shooting cancellation — used by Skip command to cancel in-progress sequence
    private CancellationTokenSource? _shootingCts;

    public CaptureViewModel(NavigationService navigationService, SessionService sessionService, ICameraService cameraService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));

        // Prepare session directory (StartViewModel does NOT call this — Event skips layout screens)
        if (string.IsNullOrEmpty(_sessionService.CurrentSession.SessionDirectory))
        {
            _sessionService.PrepareSessionDirectory();
        }
        _photosDirectory = _sessionService.CurrentSession.SessionDirectory!;
        Directory.CreateDirectory(_photosDirectory);

        // Hardcode frame path since Event has no frame selection screen
        _sessionService.CurrentSession.SelectedFrame = new Frame 
        { 
            Id = "frame6",
            Name = "Default Frame 6",
            FullImagePath = "avares://PhotoBooth.Event/Assets/finish/nen6_1.png" // Path used downstream
        };

        // Initialize camera (shared singleton — just start preview, don't create new)
        InitializeCameraAsync();
    }

    /// <summary>
    /// Initializes camera with fallback from device index 0 to 1.
    /// Subscribes to FrameReady only (NOT CameraError — no reconnect logic).
    /// Wrapped in try-catch: async void must never let exceptions escape unobserved.
    /// </summary>
    private async void InitializeCameraAsync()
    {
        try
        {
            // Execute initialization on UI thread to allow macOS camera permission prompt
            bool isInitialized = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed) return false;
                return _cameraService.InitializeBestAvailableCamera();
            });

            await Task.Run(() =>
            {
                if (_disposed) return;

                if (isInitialized)
                {
                    // Guard: subscribe only once (defensive — shared singleton camera service is reused across sessions)
                    if (!_eventSubscribed)
                    {
                        _cameraService.FrameReady += OnFrameReady;
                        // NOTE: Do NOT subscribe CameraError — no reconnect logic in Event
                        _eventSubscribed = true;
                    }

                    _cameraService.StartPreview();

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_disposed) return;
                        IsCameraReady = true;
                        StatusMessage = "Ready to capture!";
                    });
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_disposed) return;
                        StatusMessage = "Camera not found!";
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] InitializeCameraAsync unhandled error: {ex.Message}");
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;
                StatusMessage = "Camera initialization error";
            });
        }
    }

    /// <summary>
    /// Frame-ready handler with backpressure: drops frame if UI is still rendering previous one.
    /// Creates Bitmap on background thread, posts assignment to UI thread.
    /// Disposes old bitmap before assigning new.
    /// </summary>
    private void OnFrameReady(object? sender, byte[] frameBytes)
    {
        if (_disposed) return;

        // Drop frame if UI hasn't finished rendering previous one
        if (_isRenderingFrame)
        {
            var count = Interlocked.Increment(ref _droppedFrameCount);
            if (count % 100 == 0)
            {
                Console.WriteLine($"[CAMERA] Dropped {count} frames total (UI backpressure)");
            }
            return;
        }
        _isRenderingFrame = true;

        try
        {
            Bitmap bitmap;
            using (var stream = new MemoryStream(frameBytes))
            {
                bitmap = new Bitmap(stream);
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_disposed) { bitmap.Dispose(); return; }
                    var oldBitmap = CameraPreview;
                    CameraPreview = bitmap;
                    oldBitmap?.Dispose();
                }
                finally
                {
                    _isRenderingFrame = false;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Frame display error: {ex.Message}");
            _isRenderingFrame = false;
        }
    }

    /// <summary>
    /// Shooting sequence: countdown → capture → delay loop for all 6 photos.
    /// Post-loop navigation ensures user is never stuck on capture screen.
    /// Uses _shootingCts for cancellation by Skip command.
    /// </summary>
    [RelayCommand]
    private async Task StartShootingSequenceAsync()
    {
        if (!IsCameraReady || IsShootingInProgress) return;

        IsShootingInProgress = true;
        // H2 fix: Capture CTS into local variable to avoid disposal race with Dispose()
        var cts = _shootingCts = new CancellationTokenSource();
        var token = cts.Token;

        if (CurrentPhotoIndex >= TotalPhotos)
        {
            CurrentPhotoIndex = 0;
            CapturedPhotos.Clear(); // M3 fix: Clear stale paths on re-run
            _sessionService.CurrentSession.CapturedPhotoPaths.Clear(); // M4 fix: Clear session paths too
        }
        StatusMessage = "Starting capture...";

        try
        {
            while (CurrentPhotoIndex < TotalPhotos)
            {
                token.ThrowIfCancellationRequested();

                // 1. Countdown
                IsCountingDown = true;
                for (int i = CountdownDuration; i > 0; i--)
                {
                    token.ThrowIfCancellationRequested();
                    Countdown = i;
                    StatusMessage = $"Photo {CurrentPhotoIndex + 1}/{TotalPhotos} in {i}s...";
                    await Task.Delay(CountdownTickMs, token);
                }
                IsCountingDown = false;

                // 2. Capture
                await CapturePhotoAsync(token);

                // 3. Short delay between photos
                if (CurrentPhotoIndex < TotalPhotos)
                {
                    await Task.Delay(InterPhotoDelayMs, token);
                }
            }

            // H1 fix: Check _disposed before post-loop navigation — Skip may have already
            // navigated and disposed this VM between the while-loop exit and here
            if (_disposed) return;

            // Post-loop: navigate after all photos (success or partial failure)
            StatusMessage = "Complete! Transitioning...";
            await Task.Delay(TransitionDelayMs, token);
            if (_disposed) return; // Re-check after await
            await Task.Run(() => _cameraService.Deinitialize());
            _navigationService.NavigateTo<PhotoSelectViewModel>();
        }
        catch (OperationCanceledException)
        {
            // Skip command cancelled — Skip handles its own navigation
            StatusMessage = "Capture cancelled.";
            IsCountingDown = false;
        }
        finally
        {
            IsShootingInProgress = false;
            cts.Dispose();
            _shootingCts = null;
        }
    }

    private int _retryCount = 0;
    private const int MaxRetries = 3;

    /// <summary>
    /// Captures a single photo: flash effect, capture, crop to hardcoded layout6 dimensions.
    /// On failure: logs error, retries up to MaxRetries times. Skips if max retries exceeded.
    /// Navigation is NOT here — it's in StartShootingSequenceAsync post-loop.
    /// </summary>
    /// <param name="token">Cancellation token from the shooting sequence (local capture of _shootingCts, avoids disposal race H2).</param>
    private async Task CapturePhotoAsync(CancellationToken token)
    {
        try
        {
            StatusMessage = "📸 Capture!";

            // Flash effect
            IsFlashing = true;

            // Capture photo - wrapped in Task.Run to prevent blocking UI thread
            var photoPath = await Task.Run(() => 
            {
                token.ThrowIfCancellationRequested();
                var sessionDir = _sessionService.CurrentSession.SessionDirectory;
                var sessionName = _sessionService.CurrentSession.SessionFolderName;
                var photoIndex = CapturedPhotos.Count + 1;
                var customFileName = $"{DeviceConfig.EventName}_{sessionName}_{photoIndex}.jpg";
                var path = _cameraService.CapturePhoto(sessionDir, customFileName);
                token.ThrowIfCancellationRequested();
                return path;
            }, token);

            // Keep flash visible briefly
            await Task.Delay(FlashDurationMs, token);
            IsFlashing = false;

            token.ThrowIfCancellationRequested();

            CapturedPhotos.Add(photoPath);
            _sessionService.AddCapturedPhoto(photoPath);

            CurrentPhotoIndex++;
            _retryCount = 0; // Reset retries on success

            StatusMessage = $"Captured {CurrentPhotoIndex}/{TotalPhotos}";
            // NOTE: Navigation is NOT here — it's in StartShootingSequenceAsync post-loop
        }
        catch (OperationCanceledException) { throw; } // Let Skip propagate
        catch (Exception ex)
        {
            Console.WriteLine($"[CAPTURE] Photo {CurrentPhotoIndex + 1} failed: {ex.Message}");
            IsFlashing = false;
            
            _retryCount++;
            if (_retryCount >= MaxRetries)
            {
                Console.WriteLine($"[CAPTURE] Photo {CurrentPhotoIndex + 1} exceeded max retries. Skipping photo.");
                // Skip the photo to avoid infinite loop
                CurrentPhotoIndex++;
                _retryCount = 0;
                StatusMessage = $"Photo skipped after {MaxRetries} failures.";
                await Task.Delay(InterPhotoDelayMs, token); // Small delay to let user read message
            }
            else
            {
                StatusMessage = $"Photo failed, retrying... ({CurrentPhotoIndex + 1}/{TotalPhotos})";
                // Do NOT re-throw — let while loop retry the same photo
            }
        }
    }

    /// <summary>
    /// Skip command: cancels in-progress shooting, stops camera, navigates to PhotoSelectViewModel.
    /// Checks _disposed guard to prevent execution after Dispose during rapid navigation.
    /// </summary>
    [RelayCommand]
    private async Task Skip()
    {
        if (_disposed) return;
        _shootingCts?.Cancel();
        await Task.Run(() => _cameraService.Deinitialize());
        _navigationService.NavigateTo<PhotoSelectViewModel>();
    }

    /// <summary>
    /// Idempotent dispose pattern with proper ordering.
    /// 1. Cancel shooting CTS
    /// 2. Stop preview (wrapped for shutdown race)
    /// 3. Unsubscribe ONLY FrameReady (CameraError was never subscribed)
    /// 4. Set _disposed = true
    /// 5. Dispose bitmaps on UI thread
    /// Does NOT dispose _cameraService — shared singleton owned by MainWindowViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        // 1. Cancel shooting sequence if running
        _shootingCts?.Cancel();
        _shootingCts?.Dispose();
        _shootingCts = null;

        // 2. Stop preview and release hardware — wrapped for app shutdown race safety
        try { _cameraService.Deinitialize(); }
        catch (ObjectDisposedException) { }

        // 3. Unsubscribe ONLY FrameReady (CameraError was never subscribed)
        _cameraService.FrameReady -= OnFrameReady;
        _eventSubscribed = false;

        // 4. Set disposed flag AFTER unsubscribe
        _disposed = true;

        // 5. Dispose bitmaps on UI thread — camera is already stopped so no new ones will arrive
        // NOTE: Do NOT dispose _cameraService — shared singleton owned by MainWindowViewModel
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            CameraPreview?.Dispose();
            CameraPreview = null;
        });

        GC.SuppressFinalize(this);
    }
}
