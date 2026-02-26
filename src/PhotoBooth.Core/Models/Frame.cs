namespace PhotoBooth.Core.Models;

/// <summary>
/// Represents a background frame/design that goes behind the photos.
/// </summary>
public class Frame
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string FullImagePath { get; set; } = string.Empty;
    public decimal Price { get; set; } = 0;
}
