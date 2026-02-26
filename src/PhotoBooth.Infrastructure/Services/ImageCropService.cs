using System;
using System.IO;
using OpenCvSharp;

namespace PhotoBooth.Infrastructure.Services;

/// <summary>
/// Dịch vụ cắt ảnh - dễ tuỳ chỉnh
/// </summary>
public static class ImageCropService
{
    /// <summary>
    /// Cắt ảnh theo tỷ lệ mong muốn (crop từ giữa ra).
    /// Ví dụ: CropToAspectRatio("photo.jpg", 3, 4) -> crop ảnh về tỷ lệ 3:4
    /// </summary>
    /// <param name="inputPath">Đường dẫn ảnh gốc</param>
    /// <param name="widthRatio">Tỷ lệ chiều rộng (VD: 3)</param>
    /// <param name="heightRatio">Tỷ lệ chiều cao (VD: 4)</param>
    /// <param name="outputPath">Đường dẫn lưu ảnh (null = ghi đè ảnh gốc)</param>
    public static string CropToAspectRatio(string inputPath, double widthRatio, double heightRatio, string? outputPath = null)
    {
        using var src = Cv2.ImRead(inputPath);
        
        double targetAspect = widthRatio / heightRatio;
        double srcAspect = (double)src.Width / src.Height;
        
        int cropX = 0, cropY = 0, cropW = src.Width, cropH = src.Height;
        
        if (srcAspect > targetAspect)
        {
            // Ảnh rộng hơn -> cắt 2 bên trái/phải
            cropW = (int)(src.Height * targetAspect);
            cropX = (src.Width - cropW) / 2;
        }
        else if (srcAspect < targetAspect)
        {
            // Ảnh cao hơn -> cắt trên/dưới
            cropH = (int)(src.Width / targetAspect);
            cropY = (src.Height - cropH) / 2;
        }
        
        using var cropped = new Mat(src, new Rect(cropX, cropY, cropW, cropH));
        
        var savePath = outputPath ?? inputPath;
        Cv2.ImWrite(savePath, cropped, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
        
        Console.WriteLine($"[CROP] {Path.GetFileName(inputPath)}: {src.Width}x{src.Height} -> {cropW}x{cropH} (tỷ lệ {widthRatio}:{heightRatio})");
        return savePath;
    }

    /// <summary>
    /// Cắt ảnh theo kích thước pixel cụ thể (crop từ giữa ra).
    /// Ví dụ: CropToSize("photo.jpg", 800, 600)
    /// </summary>
    /// <param name="inputPath">Đường dẫn ảnh gốc</param>
    /// <param name="targetWidth">Chiều rộng mong muốn (pixel)</param>
    /// <param name="targetHeight">Chiều cao mong muốn (pixel)</param>
    /// <param name="outputPath">Đường dẫn lưu ảnh (null = ghi đè ảnh gốc)</param>
    public static string CropToSize(string inputPath, int targetWidth, int targetHeight, string? outputPath = null, int offsetX = 0)
    {
        using var src = Cv2.ImRead(inputPath);
        
        // Đảm bảo không crop lớn hơn ảnh gốc
        int cropW = Math.Min(targetWidth, src.Width);
        int cropH = Math.Min(targetHeight, src.Height);
        
        // Crop từ giữa + offset
        int cropX = (src.Width - cropW) / 2 + offsetX;
        int cropY = (src.Height - cropH) / 2;
        
        // Clamp to image bounds
        cropX = Math.Max(0, Math.Min(cropX, src.Width - cropW));
        
        using var cropped = new Mat(src, new Rect(cropX, cropY, cropW, cropH));
        
        // Resize nếu kích thước crop khác kích thước mong muốn
        Mat result;
        if (cropW != targetWidth || cropH != targetHeight)
        {
            result = new Mat();
            Cv2.Resize(cropped, result, new Size(targetWidth, targetHeight));
        }
        else
        {
            result = cropped.Clone();
        }
        
        var savePath = outputPath ?? inputPath;
        Cv2.ImWrite(savePath, result, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
        result.Dispose();
        
        Console.WriteLine($"[CROP] {Path.GetFileName(inputPath)}: {src.Width}x{src.Height} -> {targetWidth}x{targetHeight}");
        return savePath;
    }

    /// <summary>
    /// Cắt ảnh theo toạ độ tuỳ ý.
    /// Ví dụ: CropCustom("photo.jpg", 100, 50, 800, 600)
    /// </summary>
    /// <param name="inputPath">Đường dẫn ảnh gốc</param>
    /// <param name="x">Toạ độ X góc trái trên</param>
    /// <param name="y">Toạ độ Y góc trái trên</param>
    /// <param name="width">Chiều rộng vùng crop</param>
    /// <param name="height">Chiều cao vùng crop</param>
    /// <param name="outputPath">Đường dẫn lưu ảnh (null = ghi đè ảnh gốc)</param>
    public static string CropCustom(string inputPath, int x, int y, int width, int height, string? outputPath = null)
    {
        using var src = Cv2.ImRead(inputPath);
        
        // Đảm bảo không vượt quá ảnh gốc
        x = Math.Max(0, Math.Min(x, src.Width - 1));
        y = Math.Max(0, Math.Min(y, src.Height - 1));
        width = Math.Min(width, src.Width - x);
        height = Math.Min(height, src.Height - y);
        
        using var cropped = new Mat(src, new Rect(x, y, width, height));
        
        var savePath = outputPath ?? inputPath;
        Cv2.ImWrite(savePath, cropped, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
        
        Console.WriteLine($"[CROP] {Path.GetFileName(inputPath)}: Crop({x},{y},{width},{height}) from {src.Width}x{src.Height}");
        return savePath;
    }
}
