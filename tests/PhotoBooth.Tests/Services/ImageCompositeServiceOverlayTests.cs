using System;
using System.IO;
using OpenCvSharp;
using PhotoBooth.Infrastructure.Services;
using Xunit;

namespace PhotoBooth.Tests.Services;

/// <summary>
/// Tests for ImageCompositeService.OverlaySequentialNumber.
/// Q1: Covers guard clauses, edge cases, and backup behavior.
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

    [Fact]
    public void OverlaySequentialNumber_NullNumber_ReturnsNull()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlaySequentialNumber(path, null!);
        Assert.Null(result);
    }

    [Fact]
    public void OverlaySequentialNumber_EmptyNumber_ReturnsNull()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlaySequentialNumber(path, "");
        Assert.Null(result);
    }

    [Fact]
    public void OverlaySequentialNumber_NonExistentFile_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "nonexistent.png");
        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");
        Assert.Null(result);
    }

    [Fact]
    public void OverlaySequentialNumber_NullImagePath_ReturnsNull()
    {
        var result = ImageCompositeService.OverlaySequentialNumber(null!, "0516-001");
        Assert.Null(result);
    }

    [Fact]
    public void OverlaySequentialNumber_ValidInput_CreatesBackup()
    {
        var path = CreateTestImage();
        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");

        Assert.NotNull(result);
        Assert.True(File.Exists(result), "Backup file should exist");
        Assert.Contains("_original", result);
    }

    [Fact]
    public void OverlaySequentialNumber_ValidInput_ModifiesOriginalFile()
    {
        var path = CreateTestImage();
        var originalBytes = File.ReadAllBytes(path);

        ImageCompositeService.OverlaySequentialNumber(path, "0516-001");

        var modifiedBytes = File.ReadAllBytes(path);
        // Modified image should be different from original (text was drawn)
        Assert.NotEqual(originalBytes, modifiedBytes);
    }

    [Fact]
    public void OverlaySequentialNumber_ValidInput_BackupMatchesOriginal()
    {
        var path = CreateTestImage();
        var originalBytes = File.ReadAllBytes(path);

        var backupPath = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");

        Assert.NotNull(backupPath);
        var backupBytes = File.ReadAllBytes(backupPath!);
        Assert.Equal(originalBytes, backupBytes);
    }

    [Fact]
    public void OverlaySequentialNumber_TinyImage_SkipsOverlay()
    {
        // Q2: Images smaller than 100x100 should skip overlay — F1: no backup created
        var path = CreateTestImage(50, 50);
        var originalBytes = File.ReadAllBytes(path);

        var result = ImageCompositeService.OverlaySequentialNumber(path, "0516-001");

        // F1: No backup should be created for tiny images (no modification performed)
        Assert.Null(result);
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
        // Layout 6 typical size: 664x990
        var path = CreateTestImage(664, 990);
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-042"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_Layout2Size_DoesNotThrow()
    {
        // Layout 2 typical size: 682x2048
        var path = CreateTestImage(682, 2048);
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-001"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_TextWiderThanImage_DoesNotThrow()
    {
        // F4: Extremely long sequential number that exceeds image width
        var path = CreateTestImage(120, 120); // Just above 100x100 threshold
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-999999999999999"));
        Assert.Null(exception);
    }

    // F3: Helper for 4-channel BGRA images (matching production Compose output)
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
        // F3: Production images from Compose() are BGRA (CV_8UC4)
        var path = CreateTestImageBGRA();
        var exception = Record.Exception(() =>
            ImageCompositeService.OverlaySequentialNumber(path, "0516-001"));
        Assert.Null(exception);
    }

    [Fact]
    public void OverlaySequentialNumber_BGRAImage_CreatesBackupAndModifies()
    {
        // F3: Ensure overlay works correctly on 4-channel images
        var path = CreateTestImageBGRA();
        var originalBytes = File.ReadAllBytes(path);

        var backupPath = ImageCompositeService.OverlaySequentialNumber(path, "0516-042");

        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath!), "Backup should exist for BGRA image");
        var backupBytes = File.ReadAllBytes(backupPath!);
        Assert.Equal(originalBytes, backupBytes);

        var modifiedBytes = File.ReadAllBytes(path);
        Assert.NotEqual(originalBytes, modifiedBytes);
    }
}
