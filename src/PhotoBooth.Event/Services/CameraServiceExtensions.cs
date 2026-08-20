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

        // Restore the absolute original logic: try 0, then 1, then 2.
        int[] preferredOrder = { 0, 1, 2 };
        foreach (int deviceIndex in preferredOrder)
        {
            if (cameraService.Initialize(deviceIndex))
            {
                return true;
            }
        }

        return false;
    }
}
