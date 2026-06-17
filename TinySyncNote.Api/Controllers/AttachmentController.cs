using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Data;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/attachment")]
public class AttachmentController : ControllerBase
{
    private readonly AppDbContext _db;

    public AttachmentController(AppDbContext db) => _db = db;

    /// <summary>
    /// 获取附件（图片）二进制数据。无需认证，GUID 不可猜测。
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult> GetAttachment(Guid id)
    {
        var attachment = await _db.NoteAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        // 附件内容不可变 → 长时间缓存
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        return File(attachment.Data, attachment.ContentType);
    }
}
