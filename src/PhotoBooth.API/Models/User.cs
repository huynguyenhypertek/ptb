namespace PhotoBooth.API.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Device";  // SystemAdmin, StoreAdmin, Device
    public int? StoreId { get; set; }              // null = SystemAdmin (quản lý tất cả)
    public bool IsEnabled { get; set; } = true;    // Enable/Disable thiết bị từ xa
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
