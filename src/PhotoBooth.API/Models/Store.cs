namespace PhotoBooth.API.Models;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PlanType { get; set; } = "Basic";  // "Basic" or "Pro"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
