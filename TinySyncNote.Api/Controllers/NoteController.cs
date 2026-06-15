using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TinySyncNote.Api.Hubs;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize]
public class NoteController : ControllerBase
{
    private readonly INoteService _service;
    private readonly IHubContext<SyncHub> _hubContext;

    public NoteController(INoteService service, IHubContext<SyncHub> hubContext)
    {
        _service = service;
        _hubContext = hubContext;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string UserIdStr => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

    /// <summary>获取目录下所有笔记（不含正文）</summary>
    [HttpGet("by-category/{categoryId}")]
    public async Task<ActionResult<List<NoteListItem>>> GetByCategory(Guid categoryId)
    {
        try
        {
            return Ok(await _service.GetByCategoryAsync(categoryId, UserId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NoteDetailResponse>> GetById(Guid id)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id, UserId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    public async Task<ActionResult<NoteDetailResponse>> Create([FromBody] CreateNoteRequest request)
    {
        try
        {
            var note = await _service.CreateAsync(UserId, request);

            // 广播给该用户的其他设备
            await _hubContext.Clients.Group($"user_{UserIdStr}")
                .SendAsync("NoteUpdated", new
                {
                    noteId = note.Id.ToString(),
                    newVersion = note.Version,
                    action = "created"
                });

            return CreatedAtAction(nameof(GetById), new { id = note.Id }, note);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 更新笔记。携带 Version 做乐观锁冲突检测。
    /// 返回 409 + 当前版本内容 → 触发前端冲突解决。
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<NoteDetailResponse>> Update(Guid id, [FromBody] UpdateNoteRequest request)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, UserId, request);

            // 广播给该用户的其他设备
            await _hubContext.Clients.Group($"user_{UserIdStr}")
                .SendAsync("NoteUpdated", new
                {
                    noteId = id.ToString(),
                    newVersion = updated.Version,
                    action = "updated"
                });

            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            // 版本冲突 — 返回 409 和当前版本
            var current = await _service.GetByIdAsync(id, UserId);
            return Conflict(new
            {
                message = ex.Message,
                currentVersion = current
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id, UserId);

            // 广播给该用户的其他设备
            await _hubContext.Clients.Group($"user_{UserIdStr}")
                .SendAsync("NoteDeleted", new { noteId = id.ToString() });

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
