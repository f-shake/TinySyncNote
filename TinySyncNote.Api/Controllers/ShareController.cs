using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/share")]
[Authorize]
public class ShareController : ControllerBase
{
    private readonly IShareService _shareService;

    public ShareController(IShareService shareService) => _shareService = shareService;

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>分享笔记给其他用户</summary>
    [HttpPost("note/{noteId}")]
    public async Task<ActionResult<NoteShareResponse>> ShareNote(Guid noteId, [FromBody] ShareNoteRequest request)
    {
        try
        {
            var result = await _shareService.ShareNoteAsync(noteId, UserId, request.SharedWithUserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>获取某笔记的分享记录</summary>
    [HttpGet("note/{noteId}")]
    public async Task<ActionResult<List<NoteShareResponse>>> GetShares(Guid noteId)
    {
        try
        {
            return Ok(await _shareService.GetSharesForNoteAsync(noteId, UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
