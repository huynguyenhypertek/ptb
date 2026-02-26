using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SessionsController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/sessions — App chụp gửi data lên
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Session session)
    {
        session.CreatedAt = DateTime.Now;
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        
        Console.WriteLine($"[API] New session: Store={session.StoreId} Device={session.DeviceId} Layout={session.LayoutUsed} Photos={session.PhotoCount}");
        return Ok(new { message = "Session saved", id = session.Id });
    }

    // GET /api/sessions — Lấy danh sách (có thể filter theo storeId)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? storeId)
    {
        var query = _db.Sessions.AsQueryable();
        
        if (storeId.HasValue)
            query = query.Where(s => s.StoreId == storeId.Value);
        
        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
        return Ok(sessions);
    }

    // GET /api/sessions/stats — Thống kê (có thể filter theo storeId)
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? storeId)
    {
        var query = _db.Sessions.AsQueryable();
        
        if (storeId.HasValue)
            query = query.Where(s => s.StoreId == storeId.Value);
        
        var totalSessions = await query.CountAsync();
        var todaySessions = await query
            .Where(s => s.CreatedAt.Date == DateTime.Today)
            .CountAsync();
        var totalPhotos = await query.SumAsync(s => s.PhotoCount);
        var totalRevenue = await query.SumAsync(s => s.Amount);
        var todayRevenue = await query
            .Where(s => s.CreatedAt.Date == DateTime.Today)
            .SumAsync(s => s.Amount);
        
        // Thống kê theo layout
        var layoutStats = await query
            .GroupBy(s => s.LayoutUsed)
            .Select(g => new { Layout = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            totalSessions,
            todaySessions,
            totalPhotos,
            totalRevenue,
            todayRevenue,
            layoutStats
        });
    }

    // GET /api/sessions/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var session = await _db.Sessions.FindAsync(id);
        if (session == null) return NotFound();
        return Ok(session);
    }

    // DELETE /api/sessions/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await _db.Sessions.FindAsync(id);
        if (session == null) return NotFound();
        _db.Sessions.Remove(session);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }
}
