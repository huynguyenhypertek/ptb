using System;
using System.IO;
using OpenCvSharp;

namespace PhotoBooth.Infrastructure.Services;

/// <summary>
/// Service for compositing photos into frame overlays.
/// Places selected photos behind a transparent frame overlay to create the final image.
/// </summary>
public static class ImageCompositeService
{
    /// <summary>
    /// Composites photos into a frame overlay.
    /// The frame should be a PNG with transparent "holes" where photos should appear.
    /// Photos are placed at specified positions BEHIND the frame.
    /// </summary>
    /// <param name="framePath">Path to the frame overlay PNG (with transparency)</param>
    /// <param name="photoPaths">Paths to the selected photos</param>
    /// <param name="photoPositions">List of (x, y, width, height) for each photo position</param>
    /// <param name="outputPath">Path to save the final composed image</param>
    public static string Compose(string framePath, string[] photoPaths, (int x, int y, int w, int h)[] photoPositions, string outputPath)
    {
        // Load frame with alpha channel (UNCHANGED to preserve transparency)
        using var frame = Cv2.ImRead(framePath, ImreadModes.Unchanged);
        
        if (frame.Empty())
            throw new Exception($"Failed to load frame: {framePath}");
        
        // Create canvas same size as frame
        int canvasW = frame.Width;
        int canvasH = frame.Height;
        
        // Create a white base canvas (BGRA)
        using var canvas = new Mat(canvasH, canvasW, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));
        
        // Place each photo at specified position
        int count = Math.Min(photoPaths.Length, photoPositions.Length);
        for (int i = 0; i < count; i++)
        {
            var photoPath = photoPaths[i];
            var pos = photoPositions[i];
            
            if (!File.Exists(photoPath))
            {
                Console.WriteLine($"[COMPOSITE] Photo not found: {photoPath}");
                continue;
            }
            
            using var photo = Cv2.ImRead(photoPath, ImreadModes.Color);
            if (photo.Empty()) continue;
            
            // Resize photo to fit the position
            using var resized = new Mat();
            Cv2.Resize(photo, resized, new Size(pos.w, pos.h));
            
            // Convert photo to BGRA
            using var photoBGRA = new Mat();
            Cv2.CvtColor(resized, photoBGRA, ColorConversionCodes.BGR2BGRA);
            
            // Place photo on canvas at specified position (clamp to canvas bounds)
            int x = Math.Max(0, pos.x);
            int y = Math.Max(0, pos.y);
            int w = Math.Min(pos.w, canvasW - x);
            int h = Math.Min(pos.h, canvasH - y);
            
            if (w > 0 && h > 0)
            {
                var roi = new Rect(x, y, w, h);
                var photoRoi = new Rect(0, 0, w, h);
                photoBGRA[photoRoi].CopyTo(canvas[roi]);
            }
        }
        
        // Overlay the frame on top of the photos
        // Frame has alpha channel - where it's transparent, photos show through
        if (frame.Channels() == 4)
        {
            OverlayAlpha(canvas, frame);
        }
        else
        {
            // Frame has no alpha, just copy it on top
            using var frameBGRA = new Mat();
            Cv2.CvtColor(frame, frameBGRA, ColorConversionCodes.BGR2BGRA);
            frameBGRA.CopyTo(canvas);
        }
        
        // Save the final composited image
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
            
        Cv2.ImWrite(outputPath, canvas);
        Console.WriteLine($"[COMPOSITE] Final image saved: {outputPath} ({canvasW}x{canvasH})");
        
        return outputPath;
    }
    
    /// <summary>
    /// Overlay a BGRA image (with alpha) on top of a BGRA canvas.
    /// Uses alpha blending so transparent areas of the overlay show the canvas beneath.
    /// </summary>
    private static void OverlayAlpha(Mat canvas, Mat overlay)
    {
        // Ensure both are same size
        if (canvas.Size() != overlay.Size())
        {
            using var resizedOverlay = new Mat();
            Cv2.Resize(overlay, resizedOverlay, canvas.Size());
            OverlayAlphaInternal(canvas, resizedOverlay);
        }
        else
        {
            OverlayAlphaInternal(canvas, overlay);
        }
    }
    
    private static void OverlayAlphaInternal(Mat canvas, Mat overlay)
    {
        // Split overlay into channels
        Mat[] overlayChannels = Cv2.Split(overlay);
        Mat[] canvasChannels = Cv2.Split(canvas);
        
        try
        {
            // Alpha channel (0-255)
            using var alpha = overlayChannels[3];
            using var alphaFloat = new Mat();
            alpha.ConvertTo(alphaFloat, MatType.CV_32FC1, 1.0 / 255.0);
            
            using var invAlpha = new Mat();
            Cv2.Subtract(Mat.Ones(alphaFloat.Size(), MatType.CV_32FC1), alphaFloat, invAlpha);
            
            // Blend each BGR channel
            for (int c = 0; c < 3; c++)
            {
                using var canvasFloat = new Mat();
                using var overlayFloat = new Mat();
                canvasChannels[c].ConvertTo(canvasFloat, MatType.CV_32FC1);
                overlayChannels[c].ConvertTo(overlayFloat, MatType.CV_32FC1);
                
                using var blended = new Mat();
                Cv2.Add(
                    canvasFloat.Mul(invAlpha),
                    overlayFloat.Mul(alphaFloat),
                    blended
                );
                
                blended.ConvertTo(canvasChannels[c], MatType.CV_8UC1);
            }
            
            Cv2.Merge(canvasChannels, canvas);
        }
        finally
        {
            foreach (var ch in overlayChannels) ch.Dispose();
            foreach (var ch in canvasChannels) ch.Dispose();
        }
    }
}
