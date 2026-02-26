namespace PhotoBooth.Core.Models;

/// <summary>
/// Represents a photo layout template.
/// </summary>
public class Layout
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of photos to capture (user takes this many photos)
    /// </summary>
    public int CaptureCount { get; set; } = 8;
    
    /// <summary>
    /// Number of photos to select for final layout (user picks best ones)
    /// </summary>
    public int SelectCount { get; set; } = 6;
    
    public double Width { get; set; }
    public double Height { get; set; }
    public List<PhotoSlot> Slots { get; set; } = new();
}

/// <summary>
/// Defines a slot position for a photo within a layout.
/// </summary>
public class PhotoSlot
{
    public int Index { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
