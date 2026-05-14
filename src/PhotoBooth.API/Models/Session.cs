namespace PhotoBooth.API.Models;

public class Session
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = ""; // No hardcoded default — always supplied by client
    public int? StoreId { get; set; }                   // Thuộc cửa hàng nào
    public string LayoutUsed { get; set; } = "";         // layout2, layout6
    public string FrameUsed { get; set; } = "";          // nen2_1, nen6_2...
    public int PhotoCount { get; set; }                  // Số ảnh đã chọn
    public int TotalCaptured { get; set; }               // Tổng ảnh chụp
    public decimal Amount { get; set; }                   // Số tiền thu
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
