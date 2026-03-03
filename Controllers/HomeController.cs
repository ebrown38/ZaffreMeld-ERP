using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Web.Controllers;

/// <summary>
/// Main home/dashboard controller.
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<HomeController> _logger;

    public HomeController(Data.ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<HomeController> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    [HttpGet("/")]
    [HttpGet("/home")]
    public IActionResult Index()
    {
        ViewBag.Site = _app.GetSite();
        ViewBag.Version = _app.GetVersion();
        ViewBag.OpenOrders = _db.SoMstr.Count(s => s.SoStatus == "O");
        ViewBag.OpenPos = _db.PoMstr.Count(p => p.PoStatus == "O");
        ViewBag.OpenShippers = _db.ShipMstr.Count(s => s.ShStatus == "O");
        ViewBag.OpenWorkOrders = _db.PlanMstr.Count(w => w.PlanStatus == "O");
        return View();
    }

    [HttpGet("/home/error")]
    [AllowAnonymous]
    public IActionResult Error()
        => View();
}

/// <summary>
/// Login controller — web-based login page.
/// </summary>
public class AccountController : Controller
{
    private readonly IAuthService _auth;

    public AccountController(IAuthService auth) => _auth = auth;

    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromForm] string username,
        [FromForm] string password,
        [FromQuery] string? returnUrl = null)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, token, message) = await _auth.LoginAsync(username, password, ip);

        if (!success)
        {
            ViewBag.Error = message;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        Response.Cookies.Append("bs_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Redirect(returnUrl ?? "/");
    }

    [HttpPost("/logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _auth.LogoutAsync(User.Identity?.Name ?? string.Empty);
        Response.Cookies.Delete("bs_token");
        return Redirect("/login");
    }
}
