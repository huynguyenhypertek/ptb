namespace PhotoBooth.API.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";          // "Basic", "Premium"
    public string Description { get; set; } = "";    // Mô tả gói
    public decimal Price { get; set; }               // Giá/tháng
    public int MaxDevices { get; set; }              // Giới hạn số máy chụp
    public int MaxPhotosPerDay { get; set; }         // Giới hạn ảnh/ngày
    public bool HasCustomFrames { get; set; }        // Được upload khung nền riêng
    public bool HasAnalytics { get; set; }           // Được xem thống kê nâng cao
    public bool IsActive { get; set; } = true;       // Còn hoạt động không
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
