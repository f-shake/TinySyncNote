using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Data;
using TinySyncNote.Core.Models.Entities;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly AppDbContext _db;

    public UploadController(AppDbContext db) => _db = db;

    [HttpPost("image")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<ActionResult> UploadImage(
        IFormFile file,
        [FromQuery] Guid? noteId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { msg = "请选择文件" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg" };
        if (!allowed.Contains(ext))
            return BadRequest(new { msg = "不支持的图片格式" });

        var buffer = new byte[file.Length];
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var attachment = new NoteAttachment
        {
            NoteId = noteId,
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            Data = ms.ToArray(),
            FileSize = file.Length
        };

        _db.NoteAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        var url = $"/api/attachment/{attachment.Id}";

        return Ok(new
        {
            code = 0,
            data = new
            {
                errFiles = Array.Empty<string>(),
                succMap = new Dictionary<string, string> { { file.FileName, url } }
            }
        });
    }
}
