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
    
    public bool IsRunning => _isRunning;
    
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
                _capture?.Dispose();
                _capture = new VideoCapture(deviceIndex);
                
                if (!_capture.IsOpened())
                {
                    Console.WriteLine($"Failed to open camera at index {deviceIndex}");
                    return false;
                }
                
                // Set camera properties - use lower resolution for preview to reduce CPU
                _capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                _capture.Set(VideoCaptureProperties.FrameHeight, 720);
                _capture.Set(VideoCaptureProperties.Fps, 30);
                
                Console.WriteLine($"Camera initialized: {_capture.FrameWidth}x{_capture.FrameHeight}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing camera: {ex.Message}");
            return false;
        }
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
                        
                        if (frameRead && !frame.Empty())
                        {
                            consecutiveErrors = 0; // Reset khi thành công
                            
                            // Mirror horizontally for selfie view
                            Cv2.Flip(frame, frame, FlipMode.Y);
                            
                            // Convert to JPEG bytes for display
                            var bytes = frame.ToBytes(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 70));
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

    public string CapturePhoto(string outputDirectory)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(CameraService));
        if (_capture == null || !_capture.IsOpened())
        {
            throw new InvalidOperationException("Camera not initialized");
        }
        
        Directory.CreateDirectory(outputDirectory);
        
        using var frame = new Mat();
        bool frameRead = TryReadFrame(frame, CancellationToken.None);
        
        if (frameRead && !frame.Empty())
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
            
            var filename = $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
            var path = Path.Combine(outputDirectory, filename);
            
            // Save high quality JPEG
            Cv2.ImWrite(path, cropped, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            
            Console.WriteLine($"Photo saved: {path} ({cropW}x{cropH} cropped from {frameW}x{frameH})");
            return path;
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
        
        if (frameRead && !frame.Empty())
        {
            return frame.ToBytes(".jpg");
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
            _capture?.Dispose();
            _capture = null;
        }
        
        Console.WriteLine("CameraService disposed");
    }
}
