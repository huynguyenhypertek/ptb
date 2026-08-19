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

        // Try a few device indices in order. CameraService.Initialize() now
        // verifies an actual frame can be read before reporting success, so a
        // device that opens (light on) but never streams — e.g. a virtual/
        // placeholder camera device enumerated ahead of the real one — is
        // rejected here and we fall through to the next index instead of
        // getting stuck on a camera that never delivers a preview.
        for (int deviceIndex = 0; deviceIndex <= 2; deviceIndex++)
        {
            if (cameraService.Initialize(deviceIndex))
            {
                return true;
            }
        }

        return false;
    }
}
