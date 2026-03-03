using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Web.Controllers.Api;

/// <summary>
/// Authentication controller — replaces Java authServ.java servlet.
/// Handles login/logout and JWT token issuance.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService auth, ILogger<AuthController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/auth/login
    /// Body: { username, password }
    /// Returns: JWT token in cookie + response body.
    /// Mirrors Java authServ "loginAPI" case.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { success = false, message = "Username and password are required." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, token, message) = await _auth.LoginAsync(request.Username, request.Password, ip);

        if (!success)
            return Unauthorized(new { success = false, message });

        // Set secure cookie (mirrors Java session cookie approach)
        Response.Cookies.Append("bs_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Ok(new { success = true, token, message });
    }

    /// <summary>
    /// POST /api/auth/logout
    /// Clears the session cookie. Mirrors Java "kill" session header logic.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.Identity?.Name ?? string.Empty;
        await _auth.LogoutAsync(userId);
        Response.Cookies.Delete("bs_token");
        return Ok(new { success = true, message = "Logged out." });
    }

    /// <summary>GET /api/auth/ping — Validates the current session token.</summary>
    [HttpGet("ping")]
    [Authorize]
    public IActionResult Ping()
        => Ok(new
        {
            success = true,
            user = User.Identity?.Name,
            site = User.FindFirst("site")?.Value,
            userType = User.FindFirst("usertype")?.Value
        });
}

public record LoginRequest(string Username, string Password);
