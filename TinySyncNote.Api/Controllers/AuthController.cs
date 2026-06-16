using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TinySyncNote.Core.Models.DTOs;
using TinySyncNote.Core.Services;

namespace TinySyncNote.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 获取注册功能状态
    /// </summary>
    [HttpGet("registration-status")]
    public ActionResult<object> GetRegistrationStatus()
    {
        var enabled = _configuration["Registration:Enabled"] == "true";
        return Ok(new { enabled });
    }

    /// <summary>
    /// 刷新 Token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 获取当前用户信息（需认证）
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public ActionResult<UserInfo> GetProfile()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)!.Value;

        return Ok(new UserInfo
        {
            Id = userId,
            Username = username
        });
    }
}
