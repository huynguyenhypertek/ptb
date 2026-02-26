using Microsoft.AspNetCore.Mvc;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhotosController : ControllerBase
{
    private static readonly string PhotoDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public PhotosController()
    {
        if (!Directory.Exists(PhotoDir))
            Directory.CreateDirectory(PhotoDir);
    }

    // POST /api/photos/upload — Upload composite photo, return download code
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        // Generate unique code
        var code = Guid.NewGuid().ToString("N")[..8];
        var ext = Path.GetExtension(file.FileName) ?? ".png";
        var fileName = $"{code}{ext}";
        var filePath = Path.Combine(PhotoDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        Console.WriteLine($"[PHOTO] Uploaded: {fileName} ({file.Length / 1024}KB)");

        return Ok(new { code, fileName });
    }

    // GET /api/photos/{code}/image — Serve the actual image file
    [HttpGet("{code}/image")]
    public IActionResult GetImage(string code)
    {
        var files = Directory.GetFiles(PhotoDir, $"{code}.*");
        if (files.Length == 0) return NotFound();

        var filePath = files[0];
        var contentType = Path.GetExtension(filePath).ToLower() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }

    // GET /api/photos/{code}/download — Download with proper filename
    [HttpGet("{code}/download")]
    public IActionResult Download(string code)
    {
        var files = Directory.GetFiles(PhotoDir, $"{code}.*");
        if (files.Length == 0) return NotFound();

        var filePath = files[0];
        return PhysicalFile(filePath, "application/octet-stream", $"photobooth_{code}.png");
    }
}
