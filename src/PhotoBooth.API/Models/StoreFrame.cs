namespace PhotoBooth.API.Models;

/// <summary>
/// Junction table: which frames are assigned to which stores
/// </summary>
public class StoreFrame
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public int FrameId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
