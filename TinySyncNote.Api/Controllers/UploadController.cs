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
    private readonly string _storagePath;

    public UploadController(AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
    {
        _db = db;

        var mb = configuration.GetValue<int>("Upload:MaxFileSizeMB", 100);
        _maxFileSize = mb * 1024L * 1024L;

        var path = configuration.GetSection("AttachmentStorage")["Path"] ?? "App_Data/attachments";
        if (!Path.IsPathRooted(path))
            path = Path.Combine(env.ContentRootPath, path);
        _storagePath = path;
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

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();

            var attachment = new NoteAttachment
            {
                NoteId = noteId,
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                FileSize = file.Length
            };

            // 写入磁盘：{storagePath}/{id}{ext}
            var filePath = Path.Combine(_storagePath, $"{attachment.Id}{ext}");
            await System.IO.File.WriteAllBytesAsync(filePath, data);

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
