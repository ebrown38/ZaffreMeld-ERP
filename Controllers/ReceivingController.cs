using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("receiving")]
public class ReceivingController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public ReceivingController(ZaffreMeldDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? status, [FromQuery] string? q)
    {
        var query = _db.RecvMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(r => r.RvStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(r => r.RvId.Contains(q) || r.RvVend.Contains(q));
        ViewBag.Receivers = await query.OrderByDescending(r => r.RvId).Take(200).ToListAsync();
        ViewBag.Status = status; ViewBag.Q = q;
        return View();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Receiver(string id)
    {
        var recv = await _db.RecvMstr.FindAsync(id);
        if (recv == null) return NotFound();
        ViewBag.Lines = await _db.RecvDet.Where(l => l.RvdId == id).ToListAsync();
        ViewBag.Vendor = await _db.VdMstr.FindAsync(recv.RvVend);
        return View(recv);
    }

    [HttpGet("new")]
    public async Task<IActionResult> NewReceiver()
    {
        ViewBag.OpenPos = await _db.PoMstr.Where(p => p.PoStatus == "O")
            .OrderByDescending(p => p.PoNbr).Take(100).ToListAsync();
        return View();
    }
}
