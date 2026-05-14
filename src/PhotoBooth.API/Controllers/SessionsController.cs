using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SessionsController(AppDbContext db)
    {
        _db = db;
    }

    // H3-FIX: Dedicated DTO — prevents mass assignment on the public endpoint.
    // Only the fields a device is legitimately allowed to submit are exposed.
    public class CreateSessionRequest
    {
        public int StoreId { get; set; }
        public string DeviceId { get; set; } = "";
        public string LayoutUsed { get; set; } = "";
        public int PhotoCount { get; set; }
        public decimal Amount { get; set; }
    }

    // POST /api/sessions — App chụp gửi data lên
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest request)
    {
        var session = new Session
        {
            StoreId = request.StoreId,
            DeviceId = request.DeviceId,
            LayoutUsed = request.LayoutUsed,
            PhotoCount = request.PhotoCount,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow   // always server-side timestamp
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        
        Console.WriteLine($"[API] New session: Store={session.StoreId} Device={session.DeviceId} Layout={session.LayoutUsed} Photos={session.PhotoCount}");
        return Ok(new { message = "Session saved", id = session.Id });
    }

    // GET /api/sessions — Lấy danh sách (có thể filter theo storeId)
    [Authorize(Roles = "SystemAdmin,StoreAdmin")]
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
    [Authorize(Roles = "SystemAdmin,StoreAdmin")]
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? storeId)
    {
        var query = _db.Sessions.AsQueryable();
        
        if (storeId.HasValue)
            query = query.Where(s => s.StoreId == storeId.Value);
        
        var totalSessions = await query.CountAsync();
        // A2-FIX: Use UtcNow.Date to match UTC-stored CreatedAt (was DateTime.Today — local tz)
        var utcToday = DateTime.UtcNow.Date;
        var todaySessions = await query
            .Where(s => s.CreatedAt.Date == utcToday)
            .CountAsync();
        var totalPhotos = await query.SumAsync(s => s.PhotoCount);
        var totalRevenue = await query.SumAsync(s => s.Amount);
        var todayRevenue = await query
            .Where(s => s.CreatedAt.Date == utcToday)
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
    [Authorize(Roles = "SystemAdmin")]
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
