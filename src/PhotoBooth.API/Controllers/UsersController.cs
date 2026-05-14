using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "Device";
        public int? StoreId { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? Password { get; set; }   // null = không đổi password
        public string Role { get; set; } = "Device";
        public int? StoreId { get; set; }
    }

    // GET /api/users/check/{deviceId} — Check if device is enabled
    [AllowAnonymous]
    [HttpGet("check/{deviceId}")]
    public async Task<IActionResult> CheckDevice(string deviceId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == deviceId);
        if (user == null) return NotFound(new { isEnabled = false });
        return Ok(new { isEnabled = user.IsEnabled });
    }

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? storeId)
    {
        var query = _db.Users.AsQueryable();
        
        if (storeId.HasValue)
            query = query.Where(u => u.StoreId == storeId.Value);
        
        var users = await query
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Role,
                u.StoreId,
                u.IsEnabled,
                u.CreatedAt
            })
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Username)
            .ToListAsync();
        return Ok(users);
    }

    // POST /api/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        // M3-FIX: Validate role against allowed values — free-form role strings break authorization policies
        var allowedRoles = new[] { "SystemAdmin", "StoreAdmin", "Device" };
        if (!allowedRoles.Contains(request.Role))
            return BadRequest(new { message = $"Invalid role. Allowed: {string.Join(", ", allowedRoles)}" });

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new { message = "Username đã tồn tại" });
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            StoreId = request.StoreId
        };
        
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        Console.WriteLine($"[USER] Created: {user.Username} ({user.Role}) Store={user.StoreId}");
        return Ok(new { message = "User created", id = user.Id });
    }

    // PUT /api/users/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        // M3-FIX: Same role allowlist as Create
        var allowedRoles = new[] { "SystemAdmin", "StoreAdmin", "Device" };
        if (!allowedRoles.Contains(request.Role))
            return BadRequest(new { message = $"Invalid role. Allowed: {string.Join(", ", allowedRoles)}" });

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Role = request.Role;
        user.StoreId = request.StoreId;
        
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await _db.SaveChangesAsync();
        Console.WriteLine($"[USER] Updated: {user.Username} ({user.Role}) Store={user.StoreId}");
        return Ok(new { message = "Updated" });
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        Console.WriteLine($"[USER] Deleted: {user.Username}");
        return Ok(new { message = "Deleted" });
    }

    // PUT /api/users/{id}/toggle — Enable/Disable device
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleEnabled(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        
        user.IsEnabled = !user.IsEnabled;
        await _db.SaveChangesAsync();
        
        var status = user.IsEnabled ? "ENABLED" : "DISABLED";
        Console.WriteLine($"[USER] {user.Username} → {status}");
        return Ok(new { message = status, isEnabled = user.IsEnabled });
    }
}
