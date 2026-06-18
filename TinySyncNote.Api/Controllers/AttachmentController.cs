using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Data;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/attachment")]
public class AttachmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string _storagePath;

    private static readonly HashSet<string> InlineTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/bmp", "image/svg+xml", "image/avif"
    };

    public AttachmentController(AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
    {
        _db = db;

        var path = configuration.GetSection("AttachmentStorage")["Path"] ?? "App_Data/attachments";
        if (!Path.IsPathRooted(path))
            path = Path.Combine(env.ContentRootPath, path);
        _storagePath = path;
    }

    /// <summary>
    /// 获取附件（图片或文件）二进制数据。无需认证，GUID 不可猜测。
    /// 图片直接显示，非图片附件触发下载。
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult> GetAttachment(Guid id)
    {
        var attachment = await _db.NoteAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        var ext = Path.GetExtension(attachment.FileName);
        var filePath = Path.Combine(_storagePath, $"{id}{ext}");

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        // 附件内容不可变 → 长时间缓存
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        // 图片内联显示，非图片触发下载
        if (InlineTypes.Contains(attachment.ContentType))
            return PhysicalFile(filePath, attachment.ContentType);

        return PhysicalFile(filePath, attachment.ContentType, attachment.FileName);
    }
}
