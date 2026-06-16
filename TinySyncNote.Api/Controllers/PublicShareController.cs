using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
public class PublicShareController : ControllerBase
{
    private readonly IPublicShareService _publicShareService;
    private readonly IConfiguration _configuration;

    public PublicShareController(IPublicShareService publicShareService, IConfiguration configuration)
    {
        _publicShareService = publicShareService;
        _configuration = configuration;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>创建公开分享链接（需认证）</summary>
    [HttpPost("api/share/note/{noteId}/public")]
    [Authorize]
    public async Task<ActionResult<PublicShareResponse>> CreatePublicLink(Guid noteId, [FromBody] CreatePublicLinkRequest? request)
    {
        try
        {
            var result = await _publicShareService.CreatePublicLinkAsync(noteId, UserId, request?.ExpiresAt);
            var frontendUrl = _configuration.GetValue<string>("Frontend:Url");
            var frontendBase = _configuration.GetValue<string>("Frontend:BasePath") ?? "/";
            var hostUrl = !string.IsNullOrEmpty(frontendUrl) ? frontendUrl.TrimEnd('/') : $"{Request.Scheme}://{Request.Host}";
            result.ShareUrl = $"{hostUrl}{frontendBase.TrimEnd('/')}/share/{result.Token}";
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>撤销公开分享链接（需认证）</summary>
    [HttpDelete("api/share/public/{shareId}")]
    [Authorize]
    public async Task<ActionResult> RevokePublicLink(Guid shareId)
    {
        try
        {
            await _publicShareService.RevokePublicLinkAsync(shareId, UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>获取笔记的公开链接列表（需认证）</summary>
    [HttpGet("api/share/note/{noteId}/public")]
    [Authorize]
    public async Task<ActionResult<List<PublicShareResponse>>> GetPublicLinks(Guid noteId)
    {
        try
        {
            var links = await _publicShareService.GetLinksForNoteAsync(noteId, UserId);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            foreach (var link in links)
                link.ShareUrl = $"{baseUrl}/share/{link.Token}";
            return Ok(links);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>查看分享内容（无需认证）</summary>
    [HttpGet("api/share/{token}")]
    public async Task<ActionResult<PublicShareViewResponse>> ViewSharedNote(string token)
    {
        try
        {
            var result = await _publicShareService.GetSharedNoteByTokenAsync(token);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
