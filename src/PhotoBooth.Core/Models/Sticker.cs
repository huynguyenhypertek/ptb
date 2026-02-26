namespace PhotoBooth.Core.Models;

/// <summary>
/// Represents a sticker that can be placed on photos.
/// </summary>
public class Sticker
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
