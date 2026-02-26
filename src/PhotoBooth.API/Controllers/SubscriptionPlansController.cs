using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionPlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubscriptionPlansController(AppDbContext db) => _db = db;

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
    public async Task<IActionResult> Create([FromBody] SubscriptionPlan plan)
    {
        plan.CreatedAt = DateTime.Now;
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();
        Console.WriteLine($"[PLAN] Created: {plan.Name} - {plan.Price:N0}đ");
        return Ok(new { message = "Created", id = plan.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SubscriptionPlan updated)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();

        plan.Name = updated.Name;
        plan.Description = updated.Description;
        plan.Price = updated.Price;
        plan.MaxDevices = updated.MaxDevices;
        plan.MaxPhotosPerDay = updated.MaxPhotosPerDay;
        plan.HasCustomFrames = updated.HasCustomFrames;
        plan.HasAnalytics = updated.HasAnalytics;
        plan.IsActive = updated.IsActive;

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
