using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/notes/{noteId}/snapshots")]
[Authorize]
public class SnapshotController : ControllerBase
{
    private readonly ISnapshotService _service;

    public SnapshotController(ISnapshotService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<List<SnapshotListItem>>> GetAll(Guid noteId)
    {
        try
        {
            return Ok(await _service.GetByNoteAsync(noteId, UserId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{snapshotId}")]
    public async Task<ActionResult<SnapshotResponse>> GetById(Guid noteId, Guid snapshotId)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(noteId, snapshotId, UserId));
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

    /// <summary>手动创建快照</summary>
    [HttpPost]
    public async Task<ActionResult<SnapshotResponse>> Create(Guid noteId)
    {
        try
        {
            var snapshot = await _service.CreateAsync(noteId, UserId);
            return CreatedAtAction(nameof(GetById), new { noteId, snapshotId = snapshot.Id }, snapshot);
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

    /// <summary>删除快照</summary>
    [HttpDelete("{snapshotId}")]
    public async Task<ActionResult> Delete(Guid noteId, Guid snapshotId)
    {
        try
        {
            await _service.DeleteAsync(noteId, snapshotId, UserId);
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

    /// <summary>从快照恢复</summary>
    [HttpPost("{snapshotId}/restore")]
    public async Task<ActionResult<SnapshotResponse>> Restore(Guid noteId, Guid snapshotId)
    {
        try
        {
            return Ok(await _service.RestoreAsync(noteId, snapshotId, UserId));
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
