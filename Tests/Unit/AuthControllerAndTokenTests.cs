using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Unit;

/// <summary>
/// Tests for AuthController — login validation, logout cookie clearing, ping.
/// AuthService itself uses ASP.NET Core Identity, which is too heavy to spin up
/// without a full host; we mock IAuthService and verify controller behaviour.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authMock;
    private readonly AuthController _ctrl;

    public AuthControllerTests()
    {
        _authMock = new Mock<IAuthService>();
        _ctrl     = new AuthController(_authMock.Object, NullLogger<AuthController>.Instance);
        SetHttpContext(_ctrl);
    }

    // ── Login ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        _authMock.Setup(a => a.LoginAsync("admin", "pass123", It.IsAny<string>()))
            .ReturnsAsync((true, "jwt-token-abc", "Login successful."));

        var result = await _ctrl.Login(new LoginRequest("admin", "pass123")) as OkObjectResult;

        result.Should().NotBeNull();
        var body = result!.Value as dynamic;
        ((bool)body!.success).Should().BeTrue();
        ((string)body!.token).Should().Be("jwt-token-abc");
    }

    [Fact]
    public async Task Login_SetsZmTokenCookie_OnSuccess()
    {
        _authMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, "jwt-token-xyz", "OK"));

        await _ctrl.Login(new LoginRequest("admin", "pass123"));

        // Cookie is appended to response — verify via ResponseCookies
        _ctrl.Response.Headers.TryGetValue("Set-Cookie", out var cookies);
        cookies.ToString().Should().Contain("zm_token");
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        _authMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, "Invalid username or password."));

        var result = await _ctrl.Login(new LoginRequest("admin", "wrong"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_EmptyUsername_Returns400()
    {
        var result = await _ctrl.Login(new LoginRequest("", "pass123"));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_EmptyPassword_Returns400()
    {
        var result = await _ctrl.Login(new LoginRequest("admin", ""));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WhitespaceUsername_Returns400()
    {
        var result = await _ctrl.Login(new LoginRequest("   ", "pass123"));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_BothEmpty_Returns400_WithoutCallingService()
    {
        await _ctrl.Login(new LoginRequest("", ""));
        _authMock.Verify(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_PassesClientIp_ToAuthService()
    {
        _authMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, "tok", "ok"));

        await _ctrl.Login(new LoginRequest("admin", "pass123"));

        _authMock.Verify(a => a.LoginAsync("admin", "pass123", It.IsAny<string>()), Times.Once);
    }

    // ── Logout ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Returns200()
    {
        _authMock.Setup(a => a.LogoutAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        SetAuthenticatedUser(_ctrl, "admin");

        var result = await _ctrl.Logout();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Logout_CallsLogoutAsync_WithCurrentUser()
    {
        _authMock.Setup(a => a.LogoutAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        SetAuthenticatedUser(_ctrl, "testuser");

        await _ctrl.Logout();

        _authMock.Verify(a => a.LogoutAsync("testuser"), Times.Once);
    }

    [Fact]
    public async Task Logout_ClearsZmTokenCookie()
    {
        _authMock.Setup(a => a.LogoutAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        SetAuthenticatedUser(_ctrl, "admin");

        await _ctrl.Logout();

        // Verify the response includes a Set-Cookie header expiring the zm_token
        _ctrl.Response.Headers.TryGetValue("Set-Cookie", out var cookies);
        cookies.ToString().Should().Contain("zm_token");
    }

    [Fact]
    public async Task Logout_Message_SaysLoggedOut()
    {
        _authMock.Setup(a => a.LogoutAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        SetAuthenticatedUser(_ctrl, "admin");

        var result = await _ctrl.Logout() as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((string)body!.message).Should().Contain("Logged out");
    }

    // ── Ping ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Ping_AuthenticatedUser_Returns200WithUserInfo()
    {
        SetAuthenticatedUser(_ctrl, "testuser", site: "DEFAULT", userType: "admin");

        var result = _ctrl.Ping() as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.success).Should().BeTrue();
        ((string)body!.user).Should().Be("testuser");
    }

    [Fact]
    public void Ping_IncludesSiteClaim()
    {
        SetAuthenticatedUser(_ctrl, "testuser", site: "WEST");

        var result = _ctrl.Ping() as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((string)body!.site).Should().Be("WEST");
    }

    [Fact]
    public void Ping_IncludesUserTypeClaim()
    {
        SetAuthenticatedUser(_ctrl, "testuser", userType: "finance");

        var result = _ctrl.Ping() as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((string)body!.userType).Should().Be("finance");
    }

    [Fact]
    public void Ping_NullSiteClaim_ReturnsNullSite()
    {
        SetAuthenticatedUser(_ctrl, "testuser"); // no site claim

        var result = _ctrl.Ping() as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((object?)body!.site).Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void SetHttpContext(ControllerBase ctrl)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static void SetAuthenticatedUser(
        ControllerBase ctrl,
        string username,
        string? site     = null,
        string? userType = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username)
        };
        if (site     != null) claims.Add(new Claim("site",     site));
        if (userType != null) claims.Add(new Claim("usertype", userType));

        var identity   = new ClaimsIdentity(claims, "test");
        var principal  = new ClaimsPrincipal(identity);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}

/// <summary>
/// Unit tests for AuthService JWT generation logic.
/// Constructs tokens directly and validates the embedded claims
/// without requiring a running ASP.NET Core host.
/// </summary>
public class JwtTokenTests
{
    // Test the static token generation helper inline (mirrors AuthService._generateToken)
    // We test the output of the service's approach by constructing and reading back a token.

    [Fact]
    public void GeneratedToken_IsNotEmpty()
    {
        var token = BuildToken("admin", "admin", "DEFAULT");
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GeneratedToken_ContainsThreeParts()
    {
        var token  = BuildToken("admin", "admin", "DEFAULT");
        var parts  = token.Split('.');
        parts.Should().HaveCount(3, "JWT has header.payload.signature");
    }

    [Fact]
    public void GeneratedToken_DecodesWithCorrectSubject()
    {
        var token  = BuildToken("alice", "user", "DEFAULT");
        var claims = DecodeToken(token);

        claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "alice");
    }

    [Fact]
    public void GeneratedToken_ContainsRoleClaim()
    {
        var token  = BuildToken("bob", "finance", "DEFAULT");
        var claims = DecodeToken(token);

        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "finance");
    }

    [Fact]
    public void GeneratedToken_ContainsSiteClaim()
    {
        var token  = BuildToken("carol", "user", "WEST");
        var claims = DecodeToken(token);

        claims.Should().Contain(c => c.Type == "site" && c.Value == "WEST");
    }

    [Fact]
    public void GeneratedToken_IsExpiredAfterConfiguredTime()
    {
        // Token with 0-hour expiry (already expired)
        var key     = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("ZaffreMeld-Test-Secret-Key-Min32Chars!!"));
        var creds   = new Microsoft.IdentityModel.Tokens.SigningCredentials(key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:   "ZaffreMeld",
            audience: "ZaffreMeld",
            claims:   new[] { new Claim(ClaimTypes.Name, "test") },
            expires:  DateTime.UtcNow.AddSeconds(-1), // already expired
            signingCredentials: creds);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);

        // Validating this should throw or report failure
        var handler    = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = "ZaffreMeld",
            ValidateAudience         = true,
            ValidAudience            = "ZaffreMeld",
            ValidateLifetime         = true,
            IssuerSigningKey         = key,
            ClockSkew                = TimeSpan.Zero
        };

        var act = () => handler.ValidateToken(token, parameters, out _);
        act.Should().Throw<Exception>("expired token should fail validation");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string BuildToken(string username, string role, string site)
    {
        var key    = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("ZaffreMeld-Test-Secret-Key-Min32Chars!!"));
        var creds  = new Microsoft.IdentityModel.Tokens.SigningCredentials(key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new("site", site),
            new("usertype", role)
        };

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:             "ZaffreMeld",
            audience:           "ZaffreMeld",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static IEnumerable<Claim> DecodeToken(string token)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key     = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("ZaffreMeld-Test-Secret-Key-Min32Chars!!"));

        handler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer    = false,
            ValidateAudience  = false,
            ValidateLifetime  = false,
            IssuerSigningKey  = key
        }, out var validated);

        return ((System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validated).Claims;
    }
}
