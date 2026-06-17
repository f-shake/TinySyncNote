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
    private readonly long _maxFileSize;

    public UploadController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        var mb = configuration.GetValue<int>("Upload:MaxFileSizeMB", 100);
        _maxFileSize = mb * 1024L * 1024L;
    }

    [HttpPost("image")]
    [RequestSizeLimit(300 * 1024 * 1024)] // 300MB 硬限制，实际受 appsettings 控制
    public async Task<ActionResult> UploadImage(
        IFormFile file,
        [FromForm] Guid? noteId = null)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { msg = "请选择文件" });

            if (file.Length > _maxFileSize)
                return BadRequest(new { msg = $"文件大小超过限制（{_maxFileSize / (1024 * 1024)}MB）" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg" };
            if (!allowed.Contains(ext))
                return BadRequest(new { msg = "不支持的图片格式" });

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
        catch (Exception ex)
        {
            return StatusCode(500, new { msg = $"上传失败: {ex.Message}" });
        }
    }
}
