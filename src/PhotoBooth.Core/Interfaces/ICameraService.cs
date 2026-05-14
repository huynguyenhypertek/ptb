namespace PhotoBooth.Core.Interfaces;

/// <summary>
/// Interface for camera operations.
/// </summary>
public interface ICameraService : IDisposable
{
    /// <summary>
    /// Initializes the camera with the specified device index.
    /// </summary>
    bool Initialize(int deviceIndex = 0);
    
    /// <summary>
    /// Starts the camera preview.
    /// </summary>
    void StartPreview();
    
    /// <summary>
    /// Stops the camera preview.
    /// </summary>
    void StopPreview();
    
    /// <summary>
    /// Captures a single frame and saves it to the specified path.
    /// </summary>
    string CapturePhoto(string outputDirectory);
    
    /// <summary>
    /// Gets the current frame as raw bytes for display.
    /// </summary>
    byte[]? GetCurrentFrame();
    
    /// <summary>
    /// Event raised when a new frame is available for preview.
    /// </summary>
    event EventHandler<byte[]>? FrameReady;
    
    /// <summary>
    /// Event raised when camera connection is lost (read timeout or too many consecutive failures).
    /// </summary>
    event EventHandler? CameraError;
    
    /// <summary>
    /// Gets whether the camera is currently running.
    /// </summary>
    bool IsRunning { get; }
}
