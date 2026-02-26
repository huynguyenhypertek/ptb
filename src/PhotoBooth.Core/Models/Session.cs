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
    public DateTime CreatedAt { get; set; } = DateTime.Now;
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
