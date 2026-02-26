using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Data;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FramesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string _uploadDir;

    public FramesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _uploadDir = Path.Combine(env.ContentRootPath, "uploads", "frames");
        Directory.CreateDirectory(_uploadDir);
    }

    // GET /api/frames?layoutType=layout2
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? layoutType)
    {
        var query = _db.Frames.AsQueryable();
        if (!string.IsNullOrEmpty(layoutType))
            query = query.Where(f => f.LayoutType == layoutType);

        var frames = await query.OrderBy(f => f.LayoutType).ThenBy(f => f.Name).ToListAsync();
        return Ok(frames);
    }

    // GET /api/frames/{id}/image — Serve image file
    [HttpGet("{id}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var frame = await _db.Frames.FindAsync(id);
        if (frame == null) return NotFound();

        var filePath = Path.Combine(_uploadDir, frame.FileName);
        if (!System.IO.File.Exists(filePath)) return NotFound("File not found");

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, "image/png");
    }

    // POST /api/frames — Upload frame image
    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string name, [FromForm] string layoutType)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        // Generate unique filename
        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(_uploadDir, fileName);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Save to DB
        var frame = new Frame
        {
            Name = name,
            LayoutType = layoutType,
            FileName = fileName
        };
        _db.Frames.Add(frame);
        await _db.SaveChangesAsync();

        Console.WriteLine($"[FRAME] Uploaded: {name} ({layoutType}) -> {fileName}");
        return Ok(new { message = "Uploaded", id = frame.Id, fileName });
    }

    // PUT /api/frames/{id} — Update name/layout
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFrameRequest request)
    {
        var frame = await _db.Frames.FindAsync(id);
        if (frame == null) return NotFound();

        frame.Name = request.Name;
        frame.LayoutType = request.LayoutType;
        await _db.SaveChangesAsync();

        Console.WriteLine($"[FRAME] Updated: {frame.Name} ({frame.LayoutType})");
        return Ok(new { message = "Updated" });
    }

    // DELETE /api/frames/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var frame = await _db.Frames.FindAsync(id);
        if (frame == null) return NotFound();

        // Delete file
        var filePath = Path.Combine(_uploadDir, frame.FileName);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _db.Frames.Remove(frame);
        await _db.SaveChangesAsync();

        Console.WriteLine($"[FRAME] Deleted: {frame.Name}");
        return Ok(new { message = "Deleted" });
    }

    // ============ Store-Frame Assignment ============

    // GET /api/frames/store/{storeId}?layoutType=layout2
    [HttpGet("store/{storeId}")]
    public async Task<IActionResult> GetByStore(int storeId, [FromQuery] string? layoutType)
    {
        var query = from sf in _db.StoreFrames
                    join f in _db.Frames on sf.FrameId equals f.Id
                    where sf.StoreId == storeId
                    select f;

        if (!string.IsNullOrEmpty(layoutType))
            query = query.Where(f => f.LayoutType == layoutType);

        var frames = await query.OrderBy(f => f.LayoutType).ThenBy(f => f.Name).ToListAsync();
        return Ok(frames);
    }

    // GET /api/frames/{frameId}/stores — Which stores have this frame?
    [HttpGet("{frameId}/stores")]
    public async Task<IActionResult> GetAssignedStores(int frameId)
    {
        var storeIds = await _db.StoreFrames
            .Where(sf => sf.FrameId == frameId)
            .Select(sf => sf.StoreId)
            .ToListAsync();

        var stores = await _db.Stores
            .Where(s => storeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        return Ok(stores);
    }

    // POST /api/frames/assign — Assign frame to store (max 2 per layout)
    [HttpPost("assign")]
    public async Task<IActionResult> AssignToStore([FromBody] StoreFrameRequest request)
    {
        // Check if already assigned
        var exists = await _db.StoreFrames.AnyAsync(
            sf => sf.StoreId == request.StoreId && sf.FrameId == request.FrameId);
        if (exists)
            return Ok(new { message = "Already assigned" });

        // Check max 2 frames per layout per store
        var frame = await _db.Frames.FindAsync(request.FrameId);
        if (frame == null) return NotFound();

        var currentCount = await (from sf in _db.StoreFrames
                                  join f in _db.Frames on sf.FrameId equals f.Id
                                  where sf.StoreId == request.StoreId && f.LayoutType == frame.LayoutType
                                  select sf).CountAsync();

        if (currentCount >= 2)
            return BadRequest(new { message = $"Cửa hàng đã có {currentCount} khung {frame.LayoutType}, tối đa 2!" });

        _db.StoreFrames.Add(new StoreFrame
        {
            StoreId = request.StoreId,
            FrameId = request.FrameId
        });
        await _db.SaveChangesAsync();

        Console.WriteLine($"[FRAME] Assigned frame {request.FrameId} to store {request.StoreId}");
        return Ok(new { message = "Assigned" });
    }

    // DELETE /api/frames/revoke — Revoke frame from store
    [HttpDelete("revoke")]
    public async Task<IActionResult> RevokeFromStore([FromQuery] int frameId, [FromQuery] int storeId)
    {
        var sf = await _db.StoreFrames.FirstOrDefaultAsync(
            x => x.StoreId == storeId && x.FrameId == frameId);
        if (sf == null) return NotFound();

        _db.StoreFrames.Remove(sf);
        await _db.SaveChangesAsync();

        Console.WriteLine($"[FRAME] Revoked frame {frameId} from store {storeId}");
        return Ok(new { message = "Revoked" });
    }

    public class StoreFrameRequest
    {
        public int StoreId { get; set; }
        public int FrameId { get; set; }
    }

    public class UpdateFrameRequest
    {
        public string Name { get; set; } = "";
        public string LayoutType { get; set; } = "layout2";
    }
}
