using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public StoresController(AppDbContext db)
    {
        _db = db;
    }

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
    public async Task<IActionResult> Create([FromBody] Store store)
    {
        store.CreatedAt = DateTime.Now;
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Store created", id = store.Id });
    }

    // PUT /api/stores/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Store updated)
    {
        var store = await _db.Stores.FindAsync(id);
        if (store == null) return NotFound();
        
        store.Name = updated.Name;
        store.Address = updated.Address;
        store.PlanType = updated.PlanType;
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
