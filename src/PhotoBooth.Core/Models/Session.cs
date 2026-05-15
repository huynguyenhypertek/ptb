namespace PhotoBooth.Core.Models;

/// <summary>
/// Represents the current photobooth session state.
/// </summary>
public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Layout? SelectedLayout { get; set; }
    public Frame? SelectedFrame { get; set; }
    public Background? SelectedBackground { get; set; }
    public List<string> CapturedPhotoPaths { get; set; } = new();
    public List<int> SelectedPhotoIndices { get; set; } = new();
    public List<StickerPlacement> Stickers { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; }
    public string? FinalImagePath { get; set; }
    public string? QRCodeUrl { get; set; }
    /// <summary>
    /// Filesystem path for this session's photo directory.
    /// Set by CaptureViewModel when creating the session folder.
    /// </summary>
    public string? SessionDirectory { get; set; }
    /// <summary>
    /// Tên folder session (chỉ tên, không phải full path).
    /// Dùng để Google Apps Script tìm folder trên Drive.
    /// </summary>
    public string? SessionFolderName { get; set; }
    /// <summary>
    /// Google Drive folder URL pre-fetched in background during payment/capture.
    /// If set, ThankYouViewModel uses this directly instead of calling Apps Script again.
    /// </summary>
    public string? PreFetchedDriveUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a sticker placed on the final image.
/// </summary>
public class StickerPlacement
{
    public Sticker Sticker { get; set; } = null!;
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Rotation { get; set; } = 0;
}
