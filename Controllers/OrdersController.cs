using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("orders")]
public class OrdersController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    private readonly IOrderService _svc;

    public OrdersController(ZaffreMeldDbContext db, IOrderService svc)
    {
        _db = db;
        _svc = svc;
    }

    // ── Sales Orders ───────────────────────────────────────────────────────────

    [HttpGet("sales")]
    public async Task<IActionResult> SalesOrders([FromQuery] string? status, [FromQuery] string? cust, [FromQuery] string? q)
    {
        var query = _db.SoMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(s => s.SoStatus == status);
        if (!string.IsNullOrEmpty(cust))   query = query.Where(s => s.SoCust.Contains(cust));
        if (!string.IsNullOrEmpty(q))      query = query.Where(s => s.SoNbr.Contains(q) || s.SoCust.Contains(q));
        ViewBag.Orders = await query.OrderByDescending(s => s.SoNbr).Take(200).ToListAsync();
        ViewBag.Status = status; ViewBag.Q = q;
        return View();
    }

    [HttpGet("sales/{id}")]
    public async Task<IActionResult> SalesOrder(string id)
    {
        var so = await _db.SoMstr.FindAsync(id);
        if (so == null) return NotFound();
        ViewBag.Lines = await _db.SodDet.Where(l => l.SodNbr == id).OrderBy(l => l.SodLine).ToListAsync();
        ViewBag.Customer = await _db.CmMstr.FindAsync(so.SoCust);
        return View(so);
    }

    [HttpGet("sales/new")]
    public async Task<IActionResult> NewSalesOrder()
    {
        ViewBag.Customers = await _db.CmMstr.Where(c => c.CmStatus == "A").OrderBy(c => c.CmName).ToListAsync();
        ViewBag.Items = await _db.ItemMstr.Where(i => i.ItStatus == "A").OrderBy(i => i.ItItem).ToListAsync();
        return View();
    }

    [HttpPost("sales/new")]
    public async Task<IActionResult> NewSalesOrder([FromForm] SoMstr so, [FromForm] string[] items,
        [FromForm] decimal[] qtys, [FromForm] decimal[] prices)
    {
        so.SoNbr     = await GenerateNumber("SO");
        so.SoEntdate = DateTime.Today.ToString("yyyy-MM-dd");
        so.SoStatus  = "O";
        so.SoCust    ??= string.Empty;
        so.SoShip    ??= string.Empty;
        so.SoSite    ??= string.Empty;
        so.SoReqdate ??= string.Empty;
        so.SoNote    ??= string.Empty;
        so.SoUser    = User.Identity?.Name ?? string.Empty;
        so.SoCurr    ??= "USD";
        _db.SoMstr.Add(so);
        for (int i = 0; i < items.Length; i++)
        {
            if (string.IsNullOrEmpty(items[i])) continue;
            var item = await _db.ItemMstr.FindAsync(items[i]);
            _db.SodDet.Add(new SodDet
            {
                SodNbr = so.SoNbr, SodLine = i + 1,
                SodItem = items[i], SodDesc = item?.ItDesc ?? "",
                SodQty = qtys.ElementAtOrDefault(i),
                SodPrice = prices.ElementAtOrDefault(i),
                SodUom = item?.ItUom ?? "EA", SodStatus = "O"
            });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(SalesOrder), new { id = so.SoNbr });
    }

    // ── Customers ─────────────────────────────────────────────────────────────

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? q, [FromQuery] string? status)
    {
        var query = _db.CmMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.CmStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(c => c.CmCode.Contains(q) || c.CmName.Contains(q));
        ViewBag.Customers = await query.OrderBy(c => c.CmName).Take(200).ToListAsync();
        ViewBag.Q = q;
        return View();
    }

    [HttpGet("customers/{id}")]
    public async Task<IActionResult> Customer(string id)
    {
        var cust = await _db.CmMstr.FindAsync(id);
        if (cust == null) return NotFound();
        ViewBag.ShipTos = await _db.CmsDet.Where(s => s.CmsCode == id).ToListAsync();
        ViewBag.RecentOrders = await _db.SoMstr.Where(s => s.SoCust == id)
            .OrderByDescending(s => s.SoNbr).Take(10).ToListAsync();
        return View(cust);
    }

    [HttpGet("customers/new")]
    public IActionResult NewCustomer() => View(new CmMstr { CmStatus = "A", CmCurrency = "USD" });

    [HttpPost("customers/new")]
    public async Task<IActionResult> NewCustomer([FromForm] CmMstr cust)
    {
        cust.CmCrtdate  = DateTime.Today.ToString("yyyy-MM-dd");
        cust.CmCode     ??= string.Empty;
        cust.CmName     ??= string.Empty;
        cust.CmLine1    ??= string.Empty;
        cust.CmLine2    ??= string.Empty;
        cust.CmCity     ??= string.Empty;
        cust.CmState    ??= string.Empty;
        cust.CmZip      ??= string.Empty;
        cust.CmCountry  ??= string.Empty;
        cust.CmPhone    ??= string.Empty;
        cust.CmFax      ??= string.Empty;
        cust.CmEmail    ??= string.Empty;
        cust.CmContact  ??= string.Empty;
        cust.CmTerms    ??= string.Empty;
        cust.CmCurrency ??= "USD";
        cust.CmTaxcode  ??= string.Empty;
        cust.CmSite     ??= string.Empty;
        cust.CmSlsp     ??= string.Empty;
        cust.CmNote     ??= string.Empty;
        cust.CmStatus   ??= "A";
        cust.CmType     ??= string.Empty;
        cust.CmAcct     ??= string.Empty;
        cust.CmCc       ??= string.Empty;
        _db.CmMstr.Add(cust);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Customer), new { id = cust.CmCode });
    }

    [HttpPost("customers/{id}/edit")]
    public async Task<IActionResult> EditCustomer(string id, [FromForm] CmMstr cust)
    {
        cust.CmCode = id;
        _db.CmMstr.Update(cust);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Customer), new { id });
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
