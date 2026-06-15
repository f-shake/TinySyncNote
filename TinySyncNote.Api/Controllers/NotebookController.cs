using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/notebooks")]
[Authorize]
public class NotebookController : ControllerBase
{
    private readonly INotebookService _service;

    public NotebookController(INotebookService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<List<NotebookResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync(UserId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NotebookResponse>> GetById(Guid id)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id, UserId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<NotebookResponse>> Create([FromBody] CreateNotebookRequest request)
    {
        var notebook = await _service.CreateAsync(UserId, request);
        return CreatedAtAction(nameof(GetById), new { id = notebook.Id }, notebook);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<NotebookResponse>> Update(Guid id, [FromBody] UpdateNotebookRequest request)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, UserId, request));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id, UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
