using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Web.Controllers.Api;

/// <summary>
/// Orders API controller — replaces Java dataServORD.java + dataServCUS.java.
/// Covers sales orders, customers, pricing, service orders, POS.
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _svc;
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService svc, Data.ZaffreMeldDbContext db, ILogger<OrdersController> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Sales Orders ───────────────────────────────────────────────────────────

    /// <summary>GET /api/orders/sales/{nbr}</summary>
    [HttpGet("sales/{nbr}")]
    public async Task<IActionResult> GetSalesOrder(string nbr)
    {
        var so = await _svc.GetSalesOrder(nbr);
        if (so == null) return NotFound();
        var lines = await _svc.GetSalesOrderLines(nbr);
        return Ok(new { header = so, lines });
    }

    /// <summary>GET /api/orders/sales?cust=&status= — browse</summary>
    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesOrders(
        [FromQuery] string? cust = null,
        [FromQuery] string? status = null,
        [FromQuery] string? site = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var q = _db.SoMstr.AsQueryable();
        if (cust != null) q = q.Where(s => s.SoCust == cust);
        if (status != null) q = q.Where(s => s.SoStatus == status);
        if (site != null) q = q.Where(s => s.SoSite == site);

        var total = q.Count();
        var orders = q.OrderByDescending(s => s.SoEntdate)
                      .Skip((page - 1) * pageSize)
                      .Take(pageSize)
                      .ToList();
        return Ok(new { total, page, pageSize, orders });
    }

    /// <summary>POST /api/orders/sales — create SO</summary>
    [HttpPost("sales")]
    [Authorize(Roles = "admin,orders")]
    public async Task<IActionResult> CreateSalesOrder([FromBody] CreateSalesOrderRequest req)
    {
        req.Header.SoUser = User.Identity?.Name ?? string.Empty;
        var result = await _svc.CreateSalesOrder(req.Header, req.Lines);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/orders/sales/{nbr}</summary>
    [HttpPut("sales/{nbr}")]
    [Authorize(Roles = "admin,orders")]
    public async Task<IActionResult> UpdateSalesOrder(string nbr, [FromBody] SoMstr so)
    {
        if (nbr != so.SoNbr) return BadRequest("Number mismatch.");
        var result = await _svc.UpdateSalesOrder(so);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/orders/sales/{nbr}/close</summary>
    [HttpPost("sales/{nbr}/close")]
    [Authorize(Roles = "admin,orders")]
    public async Task<IActionResult> CloseSalesOrder(string nbr)
    {
        var result = await _svc.CloseOrder(nbr);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Customers ──────────────────────────────────────────────────────────────

    /// <summary>GET /api/orders/customers/{code}</summary>
    [HttpGet("customers/{code}")]
    public async Task<IActionResult> GetCustomer(string code)
    {
        var cust = await _svc.GetCustomer(code);
        return cust == null ? NotFound() : Ok(cust);
    }

    /// <summary>GET /api/orders/customers?search=</summary>
    [HttpGet("customers")]
    public async Task<IActionResult> SearchCustomers(
        [FromQuery] string search = "",
        [FromQuery] string? status = "A",
        [FromQuery] int max = 100)
    {
        var customers = await _svc.SearchCustomers(search, max);
        return Ok(customers);
    }

    /// <summary>POST /api/orders/customers</summary>
    [HttpPost("customers")]
    [Authorize(Roles = "admin,orders")]
    public async Task<IActionResult> AddCustomer([FromBody] CmMstr cust)
    {
        var result = await _svc.AddCustomer(cust);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/orders/customers/{code}</summary>
    [HttpPut("customers/{code}")]
    [Authorize(Roles = "admin,orders")]
    public async Task<IActionResult> UpdateCustomer(string code, [FromBody] CmMstr cust)
    {
        if (code != cust.CmCode) return BadRequest("Code mismatch.");
        var result = await _svc.UpdateCustomer(cust);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/orders/customers/{code}/shiptos — customer ship-to list</summary>
    [HttpGet("customers/{code}/shiptos")]
    public IActionResult GetCustomerShipTos(string code)
    {
        var shiptos = _db.CmsDet.Where(s => s.CmsCode == code).ToList();
        return Ok(shiptos);
    }

    // ── Customer Pricing ───────────────────────────────────────────────────────

    /// <summary>GET /api/orders/pricing?cust=&item= — CustPriceMaint</summary>
    [HttpGet("pricing")]
    public IActionResult GetPricing([FromQuery] string? cust = null, [FromQuery] string? item = null)
    {
        var q = _db.CprMstr.Where(p => p.CprActive);
        if (cust != null) q = q.Where(p => p.CprCust == cust);
        if (item != null) q = q.Where(p => p.CprItem == item);
        return Ok(q.ToList());
    }

    /// <summary>POST /api/orders/pricing — get effective price for cust+item+qty+date</summary>
    [HttpPost("pricing/effective")]
    public IActionResult GetEffectivePrice([FromBody] PriceRequest req)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var price = _db.CprMstr
            .Where(p => p.CprCust == req.CustCode && p.CprItem == req.Item && p.CprActive
                        && (p.CprEfffrom == "" || string.Compare(p.CprEfffrom, today) <= 0)
                        && (p.CprEffthru == "" || string.Compare(p.CprEffthru, today) >= 0)
                        && p.CprMinqty <= req.Qty)
            .OrderByDescending(p => p.CprMinqty)
            .FirstOrDefault();

        return Ok(new
        {
            cust = req.CustCode,
            item = req.Item,
            qty = req.Qty,
            price = price?.CprPrice,
            uom = price?.CprUom,
            currency = price?.CprCurrency ?? "USD",
            found = price != null
        });
    }

    // ── Customer X-Ref ─────────────────────────────────────────────────────────

    /// <summary>GET /api/orders/xref?cust=&item= — CustXrefMaint</summary>
    [HttpGet("xref")]
    public IActionResult GetCustomerXref([FromQuery] string cust, [FromQuery] string? item = null)
    {
        var q = _db.CupMstr.Where(x => x.CupCust == cust);
        if (item != null) q = q.Where(x => x.CupItem == item);
        return Ok(q.ToList());
    }

    // ── Payment Terms ──────────────────────────────────────────────────────────

    /// <summary>GET /api/orders/terms</summary>
    [HttpGet("terms")]
    public IActionResult GetTerms()
        => Ok(_db.CustTerms.Where(t => t.CutActive).OrderBy(t => t.CutCode).ToList());

    // ── Salespersons ───────────────────────────────────────────────────────────

    /// <summary>GET /api/orders/salespersons</summary>
    [HttpGet("salespersons")]
    public IActionResult GetSalespersons()
        => Ok(_db.SlspMstr.Where(s => s.SlspActive).OrderBy(s => s.SlspName).ToList());
}

public record CreateSalesOrderRequest(SoMstr Header, List<SodDet> Lines);
public record PriceRequest(string CustCode, string Item, decimal Qty);
