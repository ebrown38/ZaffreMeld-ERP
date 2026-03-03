using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Purchasing;
using ZaffreMeld.Web.Models.Vendor;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("purchasing")]
public class PurchasingController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public PurchasingController(ZaffreMeldDbContext db) => _db = db;

    // ── Purchase Orders ────────────────────────────────────────────────────────

    [HttpGet("orders")]
    public async Task<IActionResult> PurchaseOrders([FromQuery] string? status, [FromQuery] string? q)
    {
        var query = _db.PoMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.PoStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(p => p.PoNbr.Contains(q) || p.PoVend.Contains(q));
        ViewBag.Orders = await query.OrderByDescending(p => p.PoNbr).Take(200).ToListAsync();
        ViewBag.Status = status; ViewBag.Q = q;
        return View();
    }

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> PurchaseOrder(string id)
    {
        var po = await _db.PoMstr.FindAsync(id);
        if (po == null) return NotFound();
        ViewBag.Lines = await _db.PodMstr.Where(l => l.PodNbr == id).OrderBy(l => l.PodLine).ToListAsync();
        ViewBag.Vendor = await _db.VdMstr.FindAsync(po.PoVend);
        return View(po);
    }

    [HttpGet("orders/new")]
    public async Task<IActionResult> NewPurchaseOrder()
    {
        ViewBag.Vendors = await _db.VdMstr.Where(v => v.VdStatus == "A").OrderBy(v => v.VdName).ToListAsync();
        ViewBag.Items = await _db.ItemMstr.Where(i => i.ItStatus == "A").OrderBy(i => i.ItItem).ToListAsync();
        return View();
    }

    [HttpPost("orders/new")]
    public async Task<IActionResult> NewPurchaseOrder([FromForm] PoMstr po,
        [FromForm] string[] items, [FromForm] decimal[] qtys, [FromForm] decimal[] prices)
    {
        po.PoNbr     = await GenerateNumber("PO");
        po.PoEntdate = DateTime.Today.ToString("yyyy-MM-dd");
        po.PoStatus  = "O";
        po.PoUser    = User.Identity?.Name ?? string.Empty;
        po.PoVend    ??= string.Empty;
        po.PoSite    ??= string.Empty;
        po.PoReqdate ??= string.Empty;
        po.PoNote    ??= string.Empty;
        po.PoCurr    ??= "USD";
        po.PoTerms   ??= string.Empty;
        po.PoCarrier ??= string.Empty;
        po.PoShipvia ??= string.Empty;
        po.PoTaxcode ??= string.Empty;
        po.PoRevision ??= string.Empty;
        _db.PoMstr.Add(po);
        for (int i = 0; i < items.Length; i++)
        {
            if (string.IsNullOrEmpty(items[i])) continue;
            var item = await _db.ItemMstr.FindAsync(items[i]);
            _db.PodMstr.Add(new PodMstr
            {
                PodNbr = po.PoNbr, PodLine = i + 1,
                PodItem = items[i], PodDesc = item?.ItDesc ?? "",
                PodQty = qtys.ElementAtOrDefault(i),
                PodPrice = prices.ElementAtOrDefault(i),
                PodUom = item?.ItUom ?? "EA", PodStatus = "O",
                PodReqdate = po.PoReqdate
            });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(PurchaseOrder), new { id = po.PoNbr });
    }

    // ── Vendors ────────────────────────────────────────────────────────────────

    [HttpGet("/vendors")]
    public async Task<IActionResult> Vendors([FromQuery] string? q, [FromQuery] string? status)
    {
        var query = _db.VdMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(v => v.VdStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(v => v.VdAddr.Contains(q) || v.VdName.Contains(q));
        ViewBag.Vendors = await query.OrderBy(v => v.VdName).Take(200).ToListAsync();
        ViewBag.Q = q;
        return View();
    }

    [HttpGet("/vendors/{id}")]
    public async Task<IActionResult> Vendor(string id)
    {
        var vend = await _db.VdMstr.FindAsync(id);
        if (vend == null) return NotFound();
        ViewBag.RecentPos = await _db.PoMstr.Where(p => p.PoVend == id)
            .OrderByDescending(p => p.PoNbr).Take(10).ToListAsync();
        ViewBag.Pricing = await _db.VprMstr.Where(p => p.VprVend == id).Take(50).ToListAsync();
        return View(vend);
    }

    [HttpGet("/vendors/new")]
    public IActionResult NewVendor() => View(new VdMstr { VdStatus = "A", VdCurrency = "USD" });

    [HttpPost("/vendors/new")]
    public async Task<IActionResult> NewVendor([FromForm] VdMstr vend)
    {
        vend.VdCrtdate  = DateTime.Today.ToString("yyyy-MM-dd");
        vend.VdAddr     ??= string.Empty;
        vend.VdName     ??= string.Empty;
        vend.VdLine1    ??= string.Empty;
        vend.VdLine2    ??= string.Empty;
        vend.VdCity     ??= string.Empty;
        vend.VdState    ??= string.Empty;
        vend.VdZip      ??= string.Empty;
        vend.VdCountry  ??= string.Empty;
        vend.VdPhone    ??= string.Empty;
        vend.VdFax      ??= string.Empty;
        vend.VdEmail    ??= string.Empty;
        vend.VdContact  ??= string.Empty;
        vend.VdTerms    ??= string.Empty;
        vend.VdCurrency ??= "USD";
        vend.VdStatus   ??= "A";
        vend.VdNote     ??= string.Empty;
        vend.VdType     ??= string.Empty;
        vend.VdTaxid    ??= string.Empty;
        vend.VdSite     ??= string.Empty;
        _db.VdMstr.Add(vend);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Vendor), new { id = vend.VdAddr });
    }

    private async Task<string> GenerateNumber(string prefix)
    {
        var counter = await _db.Counters.FirstOrDefaultAsync(c => c.CounterPrefix == prefix);
        if (counter == null) return $"{prefix}0001";
        counter.CounterValue++;
        await _db.SaveChangesAsync();
        return $"{prefix}{counter.CounterValue.ToString().PadLeft(counter.CounterLength, '0')}";
    }
}
