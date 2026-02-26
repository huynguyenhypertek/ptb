using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PhotoBooth.API.Data;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string _jwtKey;

    public AuthController(AppDbContext db, string jwtKey)
    {
        _db = db;
        _jwtKey = jwtKey;
    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu" });
        }

        // Check if device is enabled
        if (!user.IsEnabled)
        {
            return Unauthorized(new { message = "Thiết bị đã bị vô hiệu hóa. Liên hệ Admin." });
        }

        // Lấy tên cửa hàng nếu có
        string? storeName = null;
        string planType = "Basic";
        if (user.StoreId.HasValue)
        {
            var store = await _db.Stores.FindAsync(user.StoreId.Value);
            storeName = store?.Name;
            planType = store?.PlanType ?? "Basic";
        }
        else
        {
            // SystemAdmin → Pro by default
            planType = "Pro";
        }

        // Tạo JWT token
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString()),
            new Claim("StoreId", user.StoreId?.ToString() ?? ""),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            expires: DateTime.Now.AddDays(7),
            claims: claims,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        Console.WriteLine($"[AUTH] Login: {user.Username} ({user.Role}) Store: {storeName ?? "ALL"} Plan: {planType}");
        return Ok(new
        {
            token = tokenString,
            username = user.Username,
            role = user.Role,
            storeId = user.StoreId,
            storeName = storeName,
            planType = planType,
            isEnabled = user.IsEnabled
        });
    }
}
