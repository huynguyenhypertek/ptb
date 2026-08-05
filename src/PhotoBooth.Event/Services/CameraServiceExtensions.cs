using PhotoBooth.Core.Interfaces;

namespace PhotoBooth.Event.Services;

/// <summary>
/// Extension methods for ICameraService to abstract device connection logic.
/// </summary>
public static class CameraServiceExtensions
{
    /// <summary>
    /// Abstracts device enumeration by attempting to connect to the best available camera,
    /// falling back to alternative indices if the primary device fails.
    /// </summary>
    public static bool InitializeBestAvailableCamera(this ICameraService cameraService)
    {
        if (cameraService.IsInitialized) return true;

        // Strategy: Try device 0 first (default)
        bool success = cameraService.Initialize(0);
        
        // Strategy: Fallback to device 1 if device 0 fails
        if (!success)
        {
            success = cameraService.Initialize(1);
        }
        
        return success;
    }
}
