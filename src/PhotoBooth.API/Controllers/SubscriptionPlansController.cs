using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin")]
public class SubscriptionPlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubscriptionPlansController(AppDbContext db) => _db = db;

    // W2-FIX: Dedicated DTOs — prevent mass-assignment of Id/CreatedAt
    public class CreatePlanRequest
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public int MaxDevices { get; set; }
        public int MaxPhotosPerDay { get; set; }
        public bool HasCustomFrames { get; set; }
        public bool HasAnalytics { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdatePlanRequest
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public int MaxDevices { get; set; }
        public int MaxPhotosPerDay { get; set; }
        public bool HasCustomFrames { get; set; }
        public bool HasAnalytics { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var plans = await _db.SubscriptionPlans.OrderBy(p => p.Price).ToListAsync();
        return Ok(plans);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest request)
    {
        var plan = new SubscriptionPlan
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            MaxDevices = request.MaxDevices,
            MaxPhotosPerDay = request.MaxPhotosPerDay,
            HasCustomFrames = request.HasCustomFrames,
            HasAnalytics = request.HasAnalytics,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow  // W3-FIX: UTC instead of local time
        };
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();
        Console.WriteLine($"[PLAN] Created: {plan.Name} - {plan.Price:N0}đ");
        return Ok(new { message = "Created", id = plan.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePlanRequest request)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.Price = request.Price;
        plan.MaxDevices = request.MaxDevices;
        plan.MaxPhotosPerDay = request.MaxPhotosPerDay;
        plan.HasCustomFrames = request.HasCustomFrames;
        plan.HasAnalytics = request.HasAnalytics;
        plan.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        Console.WriteLine($"[PLAN] Updated: {plan.Name}");
        return Ok(new { message = "Updated" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();

        _db.SubscriptionPlans.Remove(plan);
        await _db.SaveChangesAsync();
        Console.WriteLine($"[PLAN] Deleted: {plan.Name}");
        return Ok(new { message = "Deleted" });
    }
}
