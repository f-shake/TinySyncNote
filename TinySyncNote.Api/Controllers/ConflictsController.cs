using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/conflicts")]
[Authorize]
public class ConflictsController : ControllerBase
{
    private readonly IConflictService _service;

    public ConflictsController(IConflictService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>获取当前用户所有未解决的冲突</summary>
    [HttpGet]
    public async Task<ActionResult<List<ConflictListItem>>> GetUnresolved()
    {
        return Ok(await _service.GetUnresolvedAsync(UserId));
    }

    /// <summary>获取冲突详情（含双方内容）</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ConflictResponse>> GetById(Guid id)
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

    /// <summary>解决冲突</summary>
    [HttpPost("{id}/resolve")]
    public async Task<ActionResult<ConflictResponse>> Resolve(Guid id, [FromBody] ResolveConflictRequest request)
    {
        try
        {
            return Ok(await _service.ResolveAsync(id, UserId, request));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
