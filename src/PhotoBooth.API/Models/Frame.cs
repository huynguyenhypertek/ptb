namespace PhotoBooth.API.Models;

public class Frame
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LayoutType { get; set; } = "layout2";  // layout2 or layout6
    public string FileName { get; set; } = "";             // actual file name in uploads/frames/
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
