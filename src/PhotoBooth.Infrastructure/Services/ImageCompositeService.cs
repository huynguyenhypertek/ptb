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

            // Center-crop photo to match the target aspect ratio before resizing
            // This prevents the image from being squished (UniformToFill behavior)
            double targetAspect = (double)pos.w / pos.h;
            double photoAspect = (double)photo.Width / photo.Height;

            int cropX = 0, cropY = 0, cropW = photo.Width, cropH = photo.Height;

            if (photoAspect > targetAspect)
            {
                // Photo is wider than target -> crop sides
                cropW = (int)(photo.Height * targetAspect);
                cropX = (photo.Width - cropW) / 2;
            }
            else if (photoAspect < targetAspect)
            {
                // Photo is taller than target -> crop top/bottom
                cropH = (int)(photo.Width / targetAspect);
                cropY = (photo.Height - cropH) / 2;
            }

            using var cropped = new Mat(photo, new Rect(cropX, cropY, cropW, cropH));

            // Resize the cropped photo to exactly fit the position
            using var resized = new Mat();
            Cv2.Resize(cropped, resized, new Size(pos.w, pos.h));

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

    /// <summary>
    /// Overlay mã số thứ tự lên góc dưới phải ảnh cuối.
    /// Thêm background bán trong suốt để đọc được trên mọi nền.
    /// Saves original image as backup before modifying.
    /// </summary>
    /// <param name="imagePath">Đường dẫn ảnh cuối (sẽ ghi đè file)</param>
    /// <param name="sequentialNumber">Mã số (ví dụ: "0516-001")</param>
    /// <returns>True if overlay was successfully applied, false otherwise</returns>
    public static bool OverlaySequentialNumber(string imagePath, string sequentialNumber)
    {
        if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(sequentialNumber) || !File.Exists(imagePath))
            return false;

        // F4: Path.GetDirectoryName returns "" (not null) for relative paths like "photo.png"
        var dir = Path.GetDirectoryName(imagePath);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        var ext = Path.GetExtension(imagePath);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
        var backupPath = Path.Combine(dir, $"{nameWithoutExt}_original{ext}");

        using var image = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
        if (image.Empty())
        {
            Console.WriteLine($"[COMPOSITE] WARNING: Could not read image for overlay: {imagePath}");
            return false;
        }

        // Q2: Skip overlay on tiny images (text wouldn't be readable anyway)
        if (image.Width < 100 || image.Height < 100)
        {
            Console.WriteLine($"[COMPOSITE] Image too small for overlay ({image.Width}x{image.Height}), skipping");
            return false;
        }

        // A1/P1: Save backup of original image ONLY when we will actually modify it
        File.Copy(imagePath, backupPath, overwrite: true);

        // Font config — small but readable, especially when printed
        var fontFace = HersheyFonts.HersheySimplex;
        // D2: Chỉnh lại thật nhỏ theo yêu cầu
        double fontScale = Math.Max(0.3, image.Height * 0.0002);
        int thickness = 1; // Nét mỏng
        // F1: Use 4-component Scalar for BGRA images (Compose outputs CV_8UC4)
        var textColor = new Scalar(255, 255, 255, 255); // White, fully opaque

        // Measure text size
        int baseline;
        var textSize = Cv2.GetTextSize(sequentialNumber, fontFace, fontScale, thickness, out baseline);

        // Position: bottom-right corner, 10px margin from edge
        int padding = 8;
        int x = image.Width - textSize.Width - padding * 2 - 10;
        int y = image.Height - padding * 2 - 10;

        // Clamp position to valid range
        x = Math.Max(padding, x);
        y = Math.Max(textSize.Height + padding, y);

        // Background rectangle (semi-transparent via overlay blend)
        var bgRect = new Rect(
            x - padding,
            y - textSize.Height - padding,
            textSize.Width + padding * 2,
            textSize.Height + baseline + padding * 2
        );

        // Clamp rect to image bounds
        bgRect.X = Math.Max(0, bgRect.X);
        bgRect.Y = Math.Max(0, bgRect.Y);
        bgRect.Width = Math.Min(bgRect.Width, image.Width - bgRect.X);
        bgRect.Height = Math.Min(bgRect.Height, image.Height - bgRect.Y);

        /* 
        // F2: Guard against degenerate rect after clamping (prevents OpenCV crash)
        if (bgRect.Width > 0 && bgRect.Height > 0)
        {
            // Draw semi-transparent black background (ROI-based to avoid full-image clone)
            using var roiMat = new Mat(image, bgRect);
            using var roiOverlay = roiMat.Clone();
            // F1: Use 4-component Scalar for BGRA — alpha=255 for opaque black fill
            Cv2.Rectangle(roiOverlay, new Rect(0, 0, bgRect.Width, bgRect.Height), new Scalar(0, 0, 0, 255), -1);
            Cv2.AddWeighted(roiOverlay, 0.5, roiMat, 0.5, 0, roiMat);
        }
        */

        // Draw white text
        Cv2.PutText(image, sequentialNumber, new Point(x, y), fontFace, fontScale, textColor, thickness);

        // A3: Verify write succeeded
        var writeResult = Cv2.ImWrite(imagePath, image);
        if (!writeResult)
        {
            Console.WriteLine($"[COMPOSITE] ERROR: Failed to write overlay image to {imagePath}");
            // F2: Restore original from backup before throwing — prevent corrupted file
            try { File.Copy(backupPath, imagePath, overwrite: true); }
            catch (Exception restoreEx) { Console.WriteLine($"[COMPOSITE] ERROR: Backup restore also failed: {restoreEx.Message}"); }
            throw new IOException($"Failed to write overlay image to {imagePath}");
        }

        Console.WriteLine($"[COMPOSITE] Sequential number '{sequentialNumber}' overlaid on image");
        try { File.Delete(backupPath); } catch { }
        return true;
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

    /// <summary>
    /// Overlay QR code onto the final composite image.
    /// Scaled proportionally to 10-15% of the target image's height.
    /// Placed in the bottom-left corner with 20px margin.
    /// Saves original image as backup before modifying.
    /// </summary>
    /// <param name="imagePath">Path to the target image (will be overwritten)</param>
    /// <param name="qrBytes">QR code image bytes (PNG format from QRCoder)</param>
    /// <returns>True if overlay was successfully applied, false otherwise</returns>
    public static bool OverlayQrCode(string imagePath, byte[] qrBytes)
    {
        if (string.IsNullOrEmpty(imagePath) || qrBytes == null || qrBytes.Length == 0 || !File.Exists(imagePath))
            return false;

        var dir = Path.GetDirectoryName(imagePath);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        var ext = Path.GetExtension(imagePath);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
        var backupPath = Path.Combine(dir, $"{nameWithoutExt}_original{ext}");

        using var image = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
        if (image.Empty())
        {
            Console.WriteLine($"[COMPOSITE] WARNING: Could not read image for QR overlay: {imagePath}");
            return false;
        }

        if (image.Width < 100 || image.Height < 100)
        {
            Console.WriteLine($"[COMPOSITE] Image too small for QR overlay ({image.Width}x{image.Height}), skipping");
            return false;
        }

        File.Copy(imagePath, backupPath, overwrite: true);

        // Decode QR bytes directly into OpenCV Mat
        using var qrImage = Mat.FromImageData(qrBytes, ImreadModes.Unchanged);
        if (qrImage.Empty())
        {
            Console.WriteLine("[COMPOSITE] WARNING: Failed to decode QR code bytes");
            try { File.Delete(backupPath); } catch { }
            return false;
        }

        // Calculate scaling (10-15% of image height) -> let's use 12%
        int targetQrSize = (int)(image.Height * 0.12);
        
        // Ensure QR size is reasonable, at least 100px for reliable scanning
        targetQrSize = Math.Max(100, targetQrSize);

        using var resizedQr = new Mat();
        Cv2.Resize(qrImage, resizedQr, new Size(targetQrSize, targetQrSize), 0, 0, InterpolationFlags.Nearest);

        // Convert QR to match image channels if necessary
        using var convertedQr = new Mat();
        if (image.Channels() == 4 && resizedQr.Channels() != 4)
        {
            if (resizedQr.Channels() == 1)
                Cv2.CvtColor(resizedQr, convertedQr, ColorConversionCodes.GRAY2BGRA);
            else if (resizedQr.Channels() == 3)
                Cv2.CvtColor(resizedQr, convertedQr, ColorConversionCodes.BGR2BGRA);
        }
        else if (image.Channels() == 3 && resizedQr.Channels() != 3)
        {
             if (resizedQr.Channels() == 1)
                Cv2.CvtColor(resizedQr, convertedQr, ColorConversionCodes.GRAY2BGR);
             else if (resizedQr.Channels() == 4)
                Cv2.CvtColor(resizedQr, convertedQr, ColorConversionCodes.BGRA2BGR);
        }
        else
        {
            resizedQr.CopyTo(convertedQr);
        }

        // Position: bottom-left corner, 20px margin
        int margin = 20;
        int x = margin;
        int y = image.Height - targetQrSize - margin;

        // Clamp just in case
        x = Math.Max(0, x);
        y = Math.Max(0, y);

        // Calculate ROI width/height taking image bounds into account
        int roiW = Math.Min(targetQrSize, image.Width - x);
        int roiH = Math.Min(targetQrSize, image.Height - y);

        if (roiW > 0 && roiH > 0)
        {
            var roi = new Rect(x, y, roiW, roiH);
            var qrRoi = new Rect(0, 0, roiW, roiH);

            // Directly copy the QR code onto the image (opaque overwrite)
            // This preserves the solid white background of the QR code
            convertedQr[qrRoi].CopyTo(image[roi]);
        }

        var writeResult = Cv2.ImWrite(imagePath, image);
        if (!writeResult)
        {
            Console.WriteLine($"[COMPOSITE] ERROR: Failed to write QR overlay image to {imagePath}");
            try { File.Copy(backupPath, imagePath, overwrite: true); }
            catch (Exception restoreEx) { Console.WriteLine($"[COMPOSITE] ERROR: Backup restore also failed: {restoreEx.Message}"); }
            throw new IOException($"Failed to write QR overlay image to {imagePath}");
        }

        Console.WriteLine($"[COMPOSITE] QR code overlaid on image");
        try { File.Delete(backupPath); } catch { }
        return true;
    }
}
