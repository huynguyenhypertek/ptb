using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin")]
public class StoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public StoresController(AppDbContext db)
    {
        _db = db;
    }

    // W1-FIX: Dedicated DTOs — prevent mass-assignment of Id/CreatedAt
    public class CreateStoreRequest
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string PlanType { get; set; } = "Basic";
    }

    // W4-FIX: Dedicated DTO with same PlanType validation
    public class UpdateStoreRequest
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string PlanType { get; set; } = "Basic";
    }

    private static readonly string[] AllowedPlanTypes = { "Basic", "Pro", "Premium" };

    // GET /api/stores
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var stores = await _db.Stores
            .OrderBy(s => s.Name)
            .ToListAsync();
        return Ok(stores);
    }

    // GET /api/stores/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var store = await _db.Stores.FindAsync(id);
        if (store == null) return NotFound();
        return Ok(store);
    }

    // POST /api/stores
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequest request)
    {
        // W4-FIX: Validate PlanType against allowlist
        if (!AllowedPlanTypes.Contains(request.PlanType))
            return BadRequest(new { message = $"Invalid PlanType. Allowed: {string.Join(", ", AllowedPlanTypes)}" });

        var store = new Store
        {
            Name = request.Name,
            Address = request.Address,
            PlanType = request.PlanType,
            CreatedAt = DateTime.UtcNow  // W3-FIX: UTC instead of local time
        };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Store created", id = store.Id });
    }

    // PUT /api/stores/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStoreRequest request)
    {
        // W4-FIX: Validate PlanType against allowlist
        if (!AllowedPlanTypes.Contains(request.PlanType))
            return BadRequest(new { message = $"Invalid PlanType. Allowed: {string.Join(", ", AllowedPlanTypes)}" });

        var store = await _db.Stores.FindAsync(id);
        if (store == null) return NotFound();
        
        store.Name = request.Name;
        store.Address = request.Address;
        store.PlanType = request.PlanType;
        await _db.SaveChangesAsync();
        Console.WriteLine($"[STORE] Updated: {store.Name} - {store.Address} - Plan: {store.PlanType}");
        return Ok(new { message = "Updated" });
    }

    // DELETE /api/stores/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var store = await _db.Stores.FindAsync(id);
        if (store == null) return NotFound();
        _db.Stores.Remove(store);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }
}
