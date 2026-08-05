using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PhotoBooth.API.Data;

var builder = WebApplication.CreateBuilder(args);

// FIX #4 — Global request body size limit (10MB for image uploads)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=photobooth.db"));

// Task 1.2 — JWT key from config (NOT hardcoded)
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("JWT Key not configured! Run: dotnet user-secrets set 'Jwt:Key' 'your-secret-min-32-chars'");
if (jwtKey.Length < 32)
    throw new Exception("JWT Key must be at least 32 characters for HMAC-SHA256 security.");
if (jwtKey.Contains("CHANGE_ME") || jwtKey.Contains("SuperSecret"))
    throw new Exception("JWT Key is using a placeholder value! Set a real secret.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddSingleton(jwtKey);
builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Task 1.5 — Strict CORS: must be configured in appsettings.json, no fallback
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null || allowedOrigins.Length == 0)
            throw new Exception("CORS AllowedOrigins not configured! Set Cors:AllowedOrigins in appsettings.json");
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-create database + seed demo data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    
    // Seed demo data if empty
    if (!db.Users.Any())
    {
        // 1. Create Store
        var store1 = new PhotoBooth.API.Models.Store
        {
            Name = "Cửa hàng 1",
            Address = "123 Đường ABC, TP.HCM"
        };
        db.Stores.Add(store1);
        db.SaveChanges();
        
        // 2. Create Users
        // SystemAdmin - quản lý tất cả
        db.Users.Add(new PhotoBooth.API.Models.User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "SystemAdmin",
            StoreId = null  // không thuộc store nào
        });
        
        // StoreAdmin - quản lý cửa hàng 1
        db.Users.Add(new PhotoBooth.API.Models.User
        {
            Username = "adminCH1",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "StoreAdmin",
            StoreId = store1.Id
        });
        
        // Device - máy chụp 1, cửa hàng 1
        db.Users.Add(new PhotoBooth.API.Models.User
        {
            Username = "Pb1_Ch1",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "Device",
            StoreId = store1.Id
        });
        
        db.SaveChanges();
        Console.WriteLine("[SEED] Demo data created:");
        Console.WriteLine("  admin (SystemAdmin)");
        Console.WriteLine("  adminCH1 (StoreAdmin - Cửa hàng 1)");
        Console.WriteLine("  Pb1_Ch1 (Device - Máy 1, Cửa hàng 1)");
        Console.WriteLine("  [Default password — see source code]");
    }

    // Seed default frames if empty
    if (!db.Frames.Any())
    {
        var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "uploads", "frames");
        Directory.CreateDirectory(uploadsDir);
        
        // Tìm thư mục chứa frame assets — hoạt động cả dev lẫn packaged .app
        // Dev mode:    src/PhotoBooth.API/../PhotoBooth.UI/Assets/finish
        // Packaged:    MacOS/api/../ui_assets/finish  (copy bởi build-mac-app.sh)
        var contentRoot = app.Environment.ContentRootPath;
        var candidateAssetDirs = new[]
        {
            Path.Combine(contentRoot, "..", "PhotoBooth.UI", "Assets", "finish"),  // dev
            Path.Combine(contentRoot, "ui_assets", "finish"),                       // packaged
            Path.Combine(contentRoot, "..", "ui_assets", "finish"),                 // packaged alt
        };
        var uiAssetsDir = candidateAssetDirs.Select(Path.GetFullPath)
                                             .FirstOrDefault(Directory.Exists) ?? candidateAssetDirs[0];

        var defaultFrames = new[]
        {
            new { Name = "Khung 2 ảnh - Mẫu 1", Layout = "layout2", File = "nen2_1.png" },
            new { Name = "Khung 2 ảnh - Mẫu 2", Layout = "layout2", File = "nen2_2.png" },
            new { Name = "Khung 6 ảnh - Mẫu 1", Layout = "layout6", File = "nen6_1.png" },
            new { Name = "Khung 6 ảnh - Mẫu 2", Layout = "layout6", File = "nen6_2.png" },
        };
        
        foreach (var f in defaultFrames)
        {
            var src = Path.Combine(uiAssetsDir, f.File);
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(uploadsDir, f.File), true);
                db.Frames.Add(new PhotoBooth.API.Models.Frame
                {
                    Name = f.Name,
                    LayoutType = f.Layout,
                    FileName = f.File
                });
            }
        }
        db.SaveChanges();
        Console.WriteLine($"[SEED] Default frames created from: {uiAssetsDir}");
    }

    // Seed subscription plans
    if (!db.SubscriptionPlans.Any())
    {
        db.SubscriptionPlans.AddRange(
            new PhotoBooth.API.Models.SubscriptionPlan
            {
                Name = "Basic",
                Description = "Gói cơ bản cho cửa hàng nhỏ",
                Price = 500000,
                MaxDevices = 2,
                MaxPhotosPerDay = 100,
                HasCustomFrames = false,
                HasAnalytics = false,
                IsActive = true
            },
            new PhotoBooth.API.Models.SubscriptionPlan
            {
                Name = "Pro",
                Description = "Gói chuyên nghiệp cho cửa hàng vừa",
                Price = 1500000,
                MaxDevices = 5,
                MaxPhotosPerDay = 500,
                HasCustomFrames = true,
                HasAnalytics = false,
                IsActive = true
            },
            new PhotoBooth.API.Models.SubscriptionPlan
            {
                Name = "Premium",
                Description = "Gói cao cấp không giới hạn",
                Price = 3000000,
                MaxDevices = 999,
                MaxPhotosPerDay = 9999,
                HasCustomFrames = true,
                HasAnalytics = true,
                IsActive = true
            }
        );
        db.SaveChanges();
        Console.WriteLine("[SEED] Default subscription plans created");
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

// Health check endpoint for Admin app startup detection
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// Serve photo download page at /photos/{code}
app.MapGet("/photos/{code}", async context =>
{
    var htmlPath = Path.Combine(app.Environment.WebRootPath, "photo.html");
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(htmlPath);
});

Console.WriteLine("=================================");
Console.WriteLine("  PhotoBooth API Server Started");
Console.WriteLine("  http://localhost:5148");
Console.WriteLine("  Photos: /photos/{code}");
Console.WriteLine("=================================");

app.Run();
