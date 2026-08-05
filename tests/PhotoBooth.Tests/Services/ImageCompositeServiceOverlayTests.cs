using System;
using System.IO;
using OpenCvSharp;
using PhotoBooth.Infrastructure.Services;
using Xunit;

namespace PhotoBooth.Tests.Services;

/// <summary>
/// Tests for ImageCompositeService.OverlaySequentialNumber and OverlayQrCode.
/// </summary>
public class ImageCompositeServiceOverlayTests : IDisposable
{
    private readonly string _tempDir;

    public ImageCompositeServiceOverlayTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"photobooth_overlay_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private string CreateTestImage(int width = 800, int height = 600)
    {
        var path = Path.Combine(_tempDir, $"test_{Guid.NewGuid():N}.png");
        using var img = new Mat(height, width, MatType.CV_8UC3, new Scalar(128, 128, 128));
        Cv2.ImWrite(path, img);
        return path;
    }

    private byte[] CreateTestPngBytes(int width = 200, int height = 200)
    {
        using var img = new Mat(height, width, MatType.CV_8UC3, new Scalar(255, 255, 255));
        Cv2.Rectangle(img, new Rect(10, 10, width - 20, height - 20), new Scalar(0, 0, 0), -1);
        Cv2.ImEncode(".png", img, out byte[] buf);
        return buf;
    }

    [Fact]
    public void OverlaySequentialNumber_NullNumber_ReturnsFalse()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlaySequentialNumber(path, null!);
        Assert.False(result);
    }

    [Fact]
    public void OverlaySequentialNumber_EmptyNumber_ReturnsFalse()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlaySequentialNumber(path, "");
        Assert.False(result);
    }

    [Fact]
    public void OverlaySequentialNumber_NonExistentFile_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "nonexistent.png");
        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");
        Assert.False(result);
    }

    [Fact]
    public void OverlaySequentialNumber_NullImagePath_ReturnsFalse()
    {
        var result = ImageCompositeService.OverlaySequentialNumber(null!, "0516-001");
        Assert.False(result);
    }

    [Fact]
    public void OverlaySequentialNumber_ValidInput_ReturnsTrueAndModifies()
    {
        var path = CreateTestImage();
        var originalBytes = File.ReadAllBytes(path);

        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");

        Assert.True(result);
        var modifiedBytes = File.ReadAllBytes(path);
        Assert.NotEqual(originalBytes, modifiedBytes);
    }

    [Fact]
    public void OverlaySequentialNumber_TinyImage_SkipsOverlay()
    {
        var path = CreateTestImage(50, 50);
        var originalBytes = File.ReadAllBytes(path);

        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");

        Assert.False(result);
        var currentBytes = File.ReadAllBytes(path);
        Assert.Equal(originalBytes, currentBytes);
    }

    [Fact]
    public void OverlaySequentialNumber_LargeNumber_DoesNotThrow()
    {
        var path = CreateTestImage(2048, 2048);
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-9999"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_Layout6Size_DoesNotThrow()
    {
        var path = CreateTestImage(664, 990);
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-042"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_Layout2Size_DoesNotThrow()
    {
        var path = CreateTestImage(682, 2048);
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-001"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_TextWiderThanImage_DoesNotThrow()
    {
        var path = CreateTestImage(120, 120);
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-999999999999999"));
        Assert.Null(exception);
    }

    private string CreateTestImageBGRA(int width = 800, int height = 600)
    {
        var path = Path.Combine(_tempDir, $"test_bgra_{Guid.NewGuid():N}.png");
        using var img = new Mat(height, width, MatType.CV_8UC4, new Scalar(128, 128, 128, 255));
        Cv2.ImWrite(path, img);
        return path;
    }

    [Fact]
    public void OverlaySequentialNumber_BGRAImage_DoesNotThrow()
    {
        var path = CreateTestImageBGRA();
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-001"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_BGRAImage_ReturnsTrueAndModifies()
    {
        var path = CreateTestImageBGRA();
        var originalBytes = File.ReadAllBytes(path);

        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-042");

        Assert.True(result);
        var modifiedBytes = File.ReadAllBytes(path);
        Assert.NotEqual(originalBytes, modifiedBytes);
    }

    [Fact]
    public void OverlayQrCode_NullQrBytes_ReturnsFalse()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlayQrCode(path, null!);
        Assert.False(result);
    }

    [Fact]
    public void OverlayQrCode_EmptyQrBytes_ReturnsFalse()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlayQrCode(path, Array.Empty<byte>());
        Assert.False(result);
    }

    [Fact]
    public void OverlayQrCode_NonExistentFile_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "nonexistent.png");
        var qrBytes = CreateTestPngBytes();
        var result = ImageCompositeService.OverlayQrCode(path, qrBytes);
        Assert.False(result);
    }

    [Fact]
    public void OverlayQrCode_ValidInput_ReturnsTrueAndModifies()
    {
        var path = CreateTestImage();
        var originalBytes = File.ReadAllBytes(path);
        var qrBytes = CreateTestPngBytes();

        var result = ImageCompositeService.OverlayQrCode(path, qrBytes);

        Assert.True(result);
        var modifiedBytes = File.ReadAllBytes(path);
        Assert.NotEqual(originalBytes, modifiedBytes);
    }

    [Fact]
    public void OverlayQrCode_BGRAImage_ReturnsTrueAndModifies()
    {
        var path = CreateTestImageBGRA();
        var originalBytes = File.ReadAllBytes(path);
        var qrBytes = CreateTestPngBytes();

        var result = ImageCompositeService.OverlayQrCode(path, qrBytes);

        Assert.True(result);
        var modifiedBytes = File.ReadAllBytes(path);
        Assert.NotEqual(originalBytes, modifiedBytes);
    }

    [Fact]
    public void OverlayQrCode_TinyImage_SkipsOverlay()
    {
        var path = CreateTestImage(50, 50);
        var originalBytes = File.ReadAllBytes(path);
        var qrBytes = CreateTestPngBytes();

        var result = ImageCompositeService.OverlayQrCode(path, qrBytes);

        Assert.False(result);
        var currentBytes = File.ReadAllBytes(path);
        Assert.Equal(originalBytes, currentBytes);
    }

    [Fact]
    public void OverlayQrCode_MemoryStability_ShouldNotGrowUnboundedly()
    {
        // 1000x1000 base image as required by AC 6
        var path = CreateTestImage(1000, 1000);
        var qrBytes = CreateTestPngBytes(200, 200);

        long initialMemory = GC.GetTotalMemory(true);
        
        // Loop multiple times to ensure OpenCV Mat objects are disposed and memory doesn't leak
        for (int i = 0; i < 50; i++)
        {
            var result = ImageCompositeService.OverlayQrCode(path, qrBytes);
            Assert.True(result);
        }

        long finalMemory = GC.GetTotalMemory(true);
        long memoryDifference = finalMemory - initialMemory;

        // Allowing some threshold (e.g. 5 MB) for garbage collector overhead, 
        // but it should definitely not be a leak of 50 images * 4MB (200MB)
        Assert.True(memoryDifference < 10 * 1024 * 1024, $"Memory leak detected: grew by {memoryDifference / 1024 / 1024} MB");
    }
}
