using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotoBooth.API.Helpers;

namespace PhotoBooth.API.Controllers;

// A3-FIX: Explicit [AllowAnonymous] at controller level — all photo endpoints are public
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class PhotosController : ControllerBase
{
    // H2-FIX: Use ContentRootPath (stable across all hosting environments) instead of
    // Directory.GetCurrentDirectory() which can be wrong under IIS/nginx/systemd.
    private readonly string _photoDir;

    public PhotosController(IWebHostEnvironment env)
    {
        _photoDir = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(_photoDir);
    }

    // POST /api/photos/upload — Upload composite photo, return download code
    [AllowAnonymous]
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var error = FileValidationHelper.Validate(file);
        if (error != null) return BadRequest(new { message = error });

        // Generate unique code (lowercase for regex match)
        var code = Guid.NewGuid().ToString("N")[..8].ToLower();
        var ext = Path.GetExtension(file.FileName)!.ToLower();
        var fileName = $"{code}{ext}";
        var filePath = Path.Combine(_photoDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        Console.WriteLine($"[PHOTO] Uploaded: {fileName} ({file.Length / 1024}KB)");

        return Ok(new { code, fileName });
    }

    // GET /api/photos/{code}/image — Serve the actual image file
    [AllowAnonymous]
    [HttpGet("{code}/image")]
    public IActionResult GetImage(string code)
    {
        // Validate code format: exactly 8 lowercase hex characters
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-f0-9]{8}$"))
            return BadRequest("Invalid code format");

        // F7-FIX: Sort for deterministic selection
        var files = Directory.GetFiles(_photoDir, $"{code}.*").OrderBy(f => f).ToArray();
        if (files.Length == 0) return NotFound();

        var filePath = files[0];

        // Path containment check
        if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_photoDir)))
            return BadRequest("Invalid path");

        var contentType = Path.GetExtension(filePath).ToLower() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }

    // GET /api/photos/{code}/download — Download with proper filename
    [AllowAnonymous]
    [HttpGet("{code}/download")]
    public IActionResult Download(string code)
    {
        // Validate code format: exactly 8 lowercase hex characters
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-f0-9]{8}$"))
            return BadRequest("Invalid code format");

        // F7-FIX: Sort for deterministic selection
        var files = Directory.GetFiles(_photoDir, $"{code}.*").OrderBy(f => f).ToArray();
        if (files.Length == 0) return NotFound();

        var filePath = files[0];

        // Path containment check
        if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_photoDir)))
            return BadRequest("Invalid path");

        // L2-FIX: Use actual file extension in download name, not hardcoded .png
        var downloadExt = Path.GetExtension(filePath).ToLower();
        return PhysicalFile(filePath, "application/octet-stream", $"photobooth_{code}{downloadExt}");
    }
}
