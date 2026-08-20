using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using PhotoBooth.Core.Interfaces;

namespace PhotoBooth.Infrastructure.Services;

/// <summary>
/// Camera service implementation using OpenCvSharp4.
/// Optimized for performance with proper resource management.
/// </summary>
public class CameraService : ICameraService
{
    private VideoCapture? _capture;
    private volatile bool _isRunning;
    private volatile bool _isDisposed;
    private CancellationTokenSource? _previewCts;
    private Task? _previewTask;
    private readonly object _lock = new();

    /// <summary>
    /// OpenCV's JPEG codec relies on libjpeg's setjmp/longjmp error handling,
    /// which is not reentrant/thread-safe. StartPreview()'s background loop
    /// (frame.ToBytes) and CapturePhoto() (Cv2.ImWrite) run on different
    /// ThreadPool threads and can call into the JPEG codec at the same time.
    /// If both hit an error path simultaneously, one thread's longjmp can land
    /// on the other's jmp_buf and segfault the whole process (native SIGSEGV,
    /// unrecoverable by managed try/catch). Serialize every JPEG encode/decode
    /// call through this lock to eliminate the race.
    /// </summary>
    private readonly object _jpegLock = new();
    
    public bool IsRunning => _isRunning;
    public bool IsInitialized => _capture != null && _capture.IsOpened();
    
    public event EventHandler<byte[]>? FrameReady;
    public event EventHandler? CameraError;
    private const int ReadTimeoutMs = 2000; // 2 giây timeout cho Read()

    public bool Initialize(int deviceIndex = 0)
    {
        if (_isDisposed) return false;
        
        try
        {
            lock (_lock)
            {
                // Use Release() instead of Dispose() to avoid SIGSEGV crash
                // on some cameras (e.g. Sony ZV-E10) where AVFoundation's native
                // cleanup triggers a segfault.
                try { _capture?.Release(); } catch (Exception ex) { Console.WriteLine($"[CAMERA] Release error (non-fatal): {ex.Message}"); }
                _capture = null;

                // On macOS (both Intel x64 and Apple Silicon M1/M2/M3),
                // OpenCV requires AVFoundation backend to detect built-in
                // FaceTime cameras and most USB/HDMI capture cards.
                // We try AVFoundation first, then fall back to the default backend.
                // Use integer values directly to ensure compatibility across OpenCvSharp4 versions:
                // 1200 = CAP_AVFOUNDATION (macOS native, works on both Intel x64 and Apple Silicon)
                //   0 = CAP_ANY (let OpenCV auto-detect, cross-platform fallback)
                var backendsToTry = new (VideoCaptureAPIs api, string name)[]
                {
                    ((VideoCaptureAPIs)1200, "AVFoundation (macOS)"),
                    ((VideoCaptureAPIs)0,    "Default"),
                };

                foreach (var (api, name) in backendsToTry)
                {
                    Console.WriteLine($"[CAMERA] Trying backend: {name} (index={deviceIndex})");
                    try
                    {
                        var cap = new VideoCapture(deviceIndex, api);
                        if (cap.IsOpened())
                        {
                            _capture = cap;
                            Console.WriteLine($"[CAMERA] Opened with backend: {name}");
                            break;
                        }
                        cap.Dispose();
                        Console.WriteLine($"[CAMERA] Backend {name} could not open camera at index {deviceIndex}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CAMERA] Backend {name} threw: {ex.Message}");
                    }
                }

                if (_capture == null || !_capture.IsOpened())
                {
                    Console.WriteLine($"[CAMERA] Failed to open camera at index {deviceIndex} with any backend");
                    return false;
                }
            }

            // AVFoundation opens the capture session (indicator light turns on,
            // IsOpened()==true) as soon as the session object is created, regardless
            // of whether the requested resolution/FPS is actually supported by the
            // device — an unsupported mode silently produces zero frames instead of
            // failing outright. Try progressively safer resolutions and verify with a
            // real frame read after each, so a device that only supports e.g.
            // 1280x720 (common on some Intel Mac built-in/USB webcams) still works
            // even if a different machine's camera happens to support 1920x1080.
            var resolutionsToTry = new (int width, int height, string label)[]
            {
                (1920, 1080, "1920x1080@30"),
                (1280, 720, "1280x720@30"),
                (0, 0, "device default"), // don't call Set() at all — let the device pick
            };

            foreach (var (width, height, label) in resolutionsToTry)
            {
                lock (_lock)
                {
                    if (_capture == null) break;
                    if (width > 0)
                    {
                        _capture.Set(VideoCaptureProperties.FrameWidth, width);
                        _capture.Set(VideoCaptureProperties.FrameHeight, height);
                        _capture.Set(VideoCaptureProperties.Fps, 30);
                    }
                    Console.WriteLine($"[CAMERA] Trying resolution: {label} (reported: {_capture.FrameWidth}x{_capture.FrameHeight})");
                }

                if (_capture == null) break;

                if (WarmUpAndVerifyFrame())
                {
                    Console.WriteLine($"[CAMERA] Initialized and verified frame delivery at {label}");
                    return true;
                }

                Console.WriteLine($"[CAMERA] No frame delivered at {label}, trying next resolution");

                if (_capture == null)
                {
                    // WarmUpAndVerifyFrame's underlying TryReadFrame detected a genuine
                    // hang/timeout and already disposed the capture — device is
                    // unusable, not just a resolution mismatch. Stop trying further
                    // resolutions.
                    break;
                }
            }

            Console.WriteLine($"[CAMERA] Device {deviceIndex} opened but never delivered a frame at any resolution — treating as failed");
            lock (_lock)
            {
                _capture?.Dispose();
                _capture = null;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] Error initializing camera: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// AVFoundation's IsOpened()==true does not guarantee the capture session has
    /// begun delivering sample buffers — that can take up to a few seconds after
    /// open, especially on cold start. Without this check, StartPreview() would
    /// declare "camera ready" immediately, then silently die a few seconds later
    /// when its consecutive-read-failure threshold is hit, leaving the UI stuck
    /// showing a blank preview forever (camera light on, nothing on screen). Poll
    /// for a real, non-empty frame before declaring initialization successful.
    /// </summary>
    private bool WarmUpAndVerifyFrame()
    {
        using var frame = new Mat();
        const int warmUpAttempts = 5;
        const int warmUpDelayMs = 300;

        for (int i = 0; i < warmUpAttempts; i++)
        {
            if (TryReadFrame(frame, CancellationToken.None) && !frame.Empty())
            {
                Console.WriteLine($"[CAMERA] Warm-up: got first frame on attempt {i + 1}/{warmUpAttempts}");
                return true;
            }

            // TryReadFrame disposes _capture after a genuine hang/timeout (not just
            // a "no frame yet" false return) — no point retrying if that happened.
            if (_capture == null) break;

            Thread.Sleep(warmUpDelayMs);
        }

        return false;
    }

    public void Deinitialize()
    {
        if (_isDisposed) return;

        // Stop preview first to ensure no background task is using _capture
        if (_isRunning)
        {
            StopPreview();
        }

        // IMPORTANT: Do NOT dispose or release _capture here.
        // On macOS, VideoCapture.Dispose()/Release() with AVFoundation backend
        // can trigger a SIGSEGV in native code on certain cameras (Sony ZV-E10,
        // some HDMI capture cards). Since CameraService is a singleton, the
        // capture handle is preserved and reused by the next session's
        // Initialize() call (which checks IsInitialized first and skips
        // re-creation). The handle is only cleaned up at app shutdown via Dispose().
        Console.WriteLine("[CAMERA] Deinitialized (preview stopped, capture handle preserved).");
    }

    /// <summary>
    /// Wraps VideoCapture.Read() with a timeout to prevent indefinite blocking
    /// when camera is physically disconnected.
    /// Returns false on timeout — caller must handle reconnect.
    /// </summary>
    private bool TryReadFrame(Mat frame, CancellationToken ct)
    {
        try
        {
            var readTask = Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_capture == null || !_capture.IsOpened()) return false;
                    return _capture.Read(frame);
                }
            }, ct);

            if (readTask.Wait(ReadTimeoutMs, ct))
            {
                return readTask.Result;
            }
            
            // Timeout — camera is likely disconnected.
            Console.WriteLine("[CAMERA] Read() timed out — disposing capture to unblock");
            ForceReleaseCapture();
            // Wait for the orphaned task to finish after force-release
            try { readTask.Wait(ReadTimeoutMs); } catch (Exception) { /* swallow */ }
            // Null out the stale capture under lock so no one reuses it
            lock (_lock)
            {
                _capture?.Dispose();
                _capture = null;
            }
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] TryReadFrame error: {ex.Message}");
            return false;
        }
    }

    private bool _loggedFrameFormat;

    /// <summary>
    /// libjpeg (used internally by OpenCV's JPEG encoder) only supports 8-bit
    /// depth and 1 or 3 channel images. Some UVC/HDMI capture devices (and
    /// certain camera USB-streaming modes) hand back 16-bit or 4-channel (BGRA)
    /// frames instead. Encoding those directly corrupts libjpeg's internal
    /// state and crashes the process inside its longjmp-based error handler
    /// (native SIGSEGV that a managed try/catch cannot stop). Normalize the
    /// frame to something libjpeg can safely encode before it ever reaches
    /// ToBytes/ImWrite.
    /// </summary>
    private bool TryPrepareForEncode(Mat frame)
    {
        try
        {
            if (frame.Empty()) return false;

            if (!_loggedFrameFormat)
            {
                _loggedFrameFormat = true;
                Console.WriteLine($"[CAMERA] First frame format: {frame.Width}x{frame.Height}, channels={frame.Channels()}, depth={frame.Depth()}, type={frame.Type()}");
            }

            if (frame.Depth() != MatType.CV_8U)
            {
                frame.ConvertTo(frame, MatType.CV_8U);
            }

            if (frame.Channels() == 4)
            {
                Cv2.CvtColor(frame, frame, ColorConversionCodes.BGRA2BGR);
            }
            else if (frame.Channels() != 1 && frame.Channels() != 3)
            {
                Console.WriteLine($"[CAMERA] Unsupported channel count ({frame.Channels()}), skipping frame");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] Frame normalization failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Force-disposes the capture device to unblock any stuck Read() calls.
    /// Must be called from outside _lock.
    /// </summary>
    private void ForceReleaseCapture()
    {
        Console.WriteLine("[CAMERA] Force-releasing capture device");
        try
        {
            var capture = _capture; // Snapshot to avoid race with Dispose nulling _capture
            capture?.Release(); // Release underlying device without taking _lock
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAMERA] Force-release error: {ex.Message}");
        }
    }

    public void StartPreview()
    {
        if (_isDisposed) return;
        if (_capture == null || !_capture.IsOpened())
        {
            Console.WriteLine("Camera not initialized");
            return;
        }
        
        if (_isRunning) return;
        
        _isRunning = true;
        _previewCts = new CancellationTokenSource();
        
        _previewTask = Task.Run(async () =>
        {
            using var frame = new Mat();
            int consecutiveErrors = 0;
            const int maxConsecutiveErrors = 3; // 3 lần timeout = 6 giây
            
            try
            {
                while (!_previewCts.Token.IsCancellationRequested && _isRunning && !_isDisposed)
                {
                    try
                    {
                        bool frameRead = TryReadFrame(frame, _previewCts.Token);
                        
                        if (frameRead && TryPrepareForEncode(frame))
                        {
                            consecutiveErrors = 0;
                            byte[] bytes;
                            lock (_jpegLock)
                            {
                                // Mirror horizontally for selfie view
                                Cv2.Flip(frame, frame, FlipMode.Y);
                                
                                // Convert to JPG (quality=70) for fast display.
                                // The libjpeg SIGSEGV crash is avoided by the _jpegLock above.
                                bytes = frame.ToBytes(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 70));
                            }
                            FrameReady?.Invoke(this, bytes);
                        }
                        else
                        {
                            consecutiveErrors++;
                            Console.WriteLine($"[CAMERA] Read failure #{consecutiveErrors}/{maxConsecutiveErrors}");
                            
                            if (consecutiveErrors >= maxConsecutiveErrors)
                            {
                                Console.WriteLine("[CAMERA] Too many consecutive errors, raising CameraError");
                                break;
                            }
                        }
                        
                        // ~20fps for preview
                        await Task.Delay(50, _previewCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return; // Normal cancellation — don't raise CameraError
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Preview error: {ex.Message}");
                        consecutiveErrors++;
                        if (consecutiveErrors >= maxConsecutiveErrors)
                        {
                            Console.WriteLine("[CAMERA] Exception threshold reached");
                            break;
                        }
                    }
                }
            }
            finally
            {
                // CRITICAL: Reset _isRunning so StartPreview() can be called again on reconnect
                _isRunning = false;
                Console.WriteLine("Camera preview loop ended, _isRunning = false");
            }
            
            // Raise CameraError AFTER _isRunning is reset, outside the loop
            if (consecutiveErrors >= maxConsecutiveErrors && !_isDisposed)
            {
                CameraError?.Invoke(this, EventArgs.Empty);
            }
        }, _previewCts.Token);
    }

    public void StopPreview()
    {
        Console.WriteLine("Stopping camera preview...");
        _isRunning = false;
        
        try
        {
            _previewCts?.Cancel();
            _previewTask?.Wait(2000);
        }
        catch (AggregateException) { }
        catch (TaskCanceledException) { }
        finally
        {
            _previewCts?.Dispose();
            _previewCts = null;
            _previewTask = null;
        }
        
        Console.WriteLine("Camera preview stopped");
    }

    public string CapturePhoto(string outputDirectory, string? fileName = null)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(CameraService));
        if (_capture == null || !_capture.IsOpened())
        {
            throw new InvalidOperationException("Camera not initialized");
        }
        
        Directory.CreateDirectory(outputDirectory);
        
        using var frame = new Mat();
        bool frameRead = TryReadFrame(frame, CancellationToken.None);

        if (frameRead && TryPrepareForEncode(frame))
        {
            lock (_jpegLock)
            {
                // Mirror horizontally for selfie view
                Cv2.Flip(frame, frame, FlipMode.Y);

                // Crop to match the on-screen preview aspect ratio (1050:800 = 21:16)
                // This ensures the saved photo matches exactly what the user sees
                double targetAspect = 1050.0 / 800.0; // 1.3125
                int frameW = frame.Width;
                int frameH = frame.Height;
                double frameAspect = (double)frameW / frameH;

                int cropX = 0, cropY = 0, cropW = frameW, cropH = frameH;

                if (frameAspect > targetAspect)
                {
                    // Frame is wider than target → crop sides
                    cropW = (int)(frameH * targetAspect);
                    cropX = (frameW - cropW) / 2;
                }
                else if (frameAspect < targetAspect)
                {
                    // Frame is taller than target → crop top/bottom
                    cropH = (int)(frameW / targetAspect);
                    cropY = (frameH - cropH) / 2;
                }

                using var cropped = new Mat(frame, new Rect(cropX, cropY, cropW, cropH));

                var finalFileName = string.IsNullOrWhiteSpace(fileName)
                    ? $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg"
                    : fileName;

                var path = Path.Combine(outputDirectory, finalFileName);

                // Save JPG (fast). The libjpeg crash is prevented by _jpegLock
                lock (_jpegLock)
                {
                    Cv2.ImWrite(path, cropped, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                }

                Console.WriteLine($"Photo saved: {path} ({cropW}x{cropH} cropped from {frameW}x{frameH})");
                return path;
            }
        }
        
        throw new Exception("Failed to capture frame");
    }

    public byte[]? GetCurrentFrame()
    {
        if (_isDisposed) return null;
        if (_capture == null || !_capture.IsOpened())
        {
            return null;
        }
        
        using var frame = new Mat();
        bool frameRead = TryReadFrame(frame, CancellationToken.None);

        if (frameRead && TryPrepareForEncode(frame))
        {
            lock (_jpegLock)
            {
                return frame.ToBytes(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        Console.WriteLine("Disposing CameraService...");
        
        StopPreview();
        
        lock (_lock)
        {
            // Wrap in try-catch: AVFoundation VideoCapture cleanup can SIGSEGV
            // on some camera hardware. At app shutdown this is acceptable.
            try
            {
                _capture?.Release();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA] Dispose release error: {ex.Message}");
            }
            _capture = null;
        }
        
        Console.WriteLine("CameraService disposed");
    }
}
