using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class UserSettingController : ControllerBase
{
    private readonly IUserSettingService _service;

    public UserSettingController(IUserSettingService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll()
    {
        var settings = await _service.GetAllAsync(UserId);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult> Update([FromBody] Dictionary<string, string> settings)
    {
        await _service.SetBatchAsync(UserId, settings);
        return NoContent();
    }
}
