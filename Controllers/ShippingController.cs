using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("shipping")]
public class ShippingController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public ShippingController(ZaffreMeldDbContext db) => _db = db;

    [HttpGet("shippers")]
    public async Task<IActionResult> Shippers([FromQuery] string? status, [FromQuery] string? q)
    {
        var query = _db.ShipMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(s => s.ShStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(s => s.ShId.Contains(q) || s.ShCust.Contains(q));
        ViewBag.Shippers = await query.OrderByDescending(s => s.ShId).Take(200).ToListAsync();
        ViewBag.Status = status; ViewBag.Q = q;
        return View();
    }

    [HttpGet("shippers/{id}")]
    public async Task<IActionResult> Shipper(string id)
    {
        var shipper = await _db.ShipMstr.FindAsync(id);
        if (shipper == null) return NotFound();
        ViewBag.Lines = await _db.ShipDet.Where(l => l.ShdId == id).OrderBy(l => l.ShdLine).ToListAsync();
        ViewBag.Customer = await _db.CmMstr.FindAsync(shipper.ShCust);
        return View(shipper);
    }

    [HttpGet("shippers/new")]
    public async Task<IActionResult> NewShipper()
    {
        ViewBag.Customers = await _db.CmMstr.Where(c => c.CmStatus == "A").OrderBy(c => c.CmName).ToListAsync();
        ViewBag.SalesOrders = await _db.SoMstr.Where(s => s.SoStatus == "O").OrderByDescending(s => s.SoNbr).Take(100).ToListAsync();
        ViewBag.Carriers = await _db.CarMstr.Where(c => c.CarActive).OrderBy(c => c.CarId).ToListAsync();
        return View();
    }
}
