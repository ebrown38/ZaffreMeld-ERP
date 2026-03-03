using ZaffreMeld.Web.Models.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZaffreMeld.Web.Data;

namespace ZaffreMeld.Web.Controllers;

[Authorize(Roles = "admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public AdminController(ZaffreMeldDbContext db) => _db = db;

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        ViewBag.Users = await _db.ZaffreMeldUsers.OrderBy(u => u.UserId).ToListAsync();
        return View();
    }

    [HttpGet("sites")]
    public async Task<IActionResult> Sites()
    {
        ViewBag.Sites = await _db.Sites.OrderBy(s => s.SiteSite).ToListAsync();
        return View();
    }

    [HttpGet("codes")]
    public async Task<IActionResult> Codes([FromQuery] string? type)
    {
        var query = _db.CodeMstr.AsQueryable();
        if (!string.IsNullOrEmpty(type)) query = query.Where(c => c.CodeCode == type);
        ViewBag.Codes = await query.OrderBy(c => c.CodeCode).ToListAsync();
        ViewBag.Types = await _db.CodeMstr.Select(c => c.CodeCode).Distinct().OrderBy(t => t).ToListAsync();
        ViewBag.SelectedType = type;
        return View();
    }

    [HttpGet("changelog")]
    public async Task<IActionResult> ChangeLog([FromQuery] string? table, [FromQuery] string? user, [FromQuery] string? from)
    {
        var query = _db.ChangeLogs.AsQueryable();
        if (!string.IsNullOrEmpty(table)) query = query.Where(c => c.ClTable == table);
        if (!string.IsNullOrEmpty(user))  query = query.Where(c => c.ClUser == user);
        if (!string.IsNullOrEmpty(from))  query = query.Where(c => c.ClTimestamp >= DateTime.Parse(from));
        ViewBag.Logs = await query.OrderByDescending(c => c.ClTimestamp).Take(500).ToListAsync();
        ViewBag.Tables = await _db.ChangeLogs.Select(c => c.ClTable).Distinct().OrderBy(t => t).ToListAsync();
        ViewBag.Table = table; ViewBag.FilterUser = user;
        return View();
    }
}
