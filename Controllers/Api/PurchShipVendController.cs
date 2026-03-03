using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZaffreMeld.Web.Models.Purchasing;
using ZaffreMeld.Web.Models.Shipping;
using ZaffreMeld.Web.Models.Vendor;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Web.Controllers.Api;

// ─────────────────────────────────────────────────────────────────────────────
// PURCHASING
// Replaces: dataServPUR.java
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/purchasing")]
[Authorize]
public class PurchasingController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<PurchasingController> _logger;

    public PurchasingController(Data.ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<PurchasingController> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    [HttpGet("orders/{nbr}")]
    public IActionResult GetPurchaseOrder(string nbr)
    {
        var po = _db.PoMstr.Find(nbr);
        if (po == null) return NotFound();
        var lines = _db.PodMstr.Where(l => l.PodNbr == nbr).OrderBy(l => l.PodLine).ToList();
        var addr = _db.PoAddr.FirstOrDefault(a => a.PoaNbr == nbr);
        var meta = _db.PoMeta.Where(m => m.PomNbr == nbr).ToList();
        return Ok(new { header = po, lines, addr, meta });
    }

    [HttpGet("orders")]
    public IActionResult GetPurchaseOrders(
        [FromQuery] string? vend = null,
        [FromQuery] string? status = null,
        [FromQuery] string? site = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var q = _db.PoMstr.AsQueryable();
        if (vend != null) q = q.Where(p => p.PoVend == vend);
        if (status != null) q = q.Where(p => p.PoStatus == status);
        if (site != null) q = q.Where(p => p.PoSite == site);
        var total = q.Count();
        var orders = q.OrderByDescending(p => p.PoEntdate).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { total, page, pageSize, orders });
    }

    [HttpPost("orders")]
    [Authorize(Roles = "admin,purchasing")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePoRequest req)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrEmpty(req.Header.PoNbr))
                req.Header.PoNbr = await _app.GetNextDocumentNumber("PO");

            req.Header.PoEntdate = DateTime.Today.ToString("yyyy-MM-dd");
            req.Header.PoStatus = "O";
            req.Header.PoUser = User.Identity?.Name ?? string.Empty;

            int lineNum = 10;
            foreach (var line in req.Lines)
            {
                line.PodNbr = req.Header.PoNbr;
                if (line.PodLine == 0) { line.PodLine = lineNum; lineNum += 10; }
            }

            req.Header.PoTotalamt = req.Lines.Sum(l => l.PodQty * l.PodPrice);
            _db.PoMstr.Add(req.Header);
            _db.PodMstr.AddRange(req.Lines);
            if (req.Addr != null) { req.Addr.PoaNbr = req.Header.PoNbr; _db.PoAddr.Add(req.Addr); }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { success = true, poNbr = req.Header.PoNbr });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error creating PO");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("orders/{nbr}")]
    [Authorize(Roles = "admin,purchasing")]
    public async Task<IActionResult> UpdatePurchaseOrder(string nbr, [FromBody] PoMstr po)
    {
        if (nbr != po.PoNbr) return BadRequest("Number mismatch.");
        _db.PoMstr.Update(po);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost("orders/{nbr}/close")]
    [Authorize(Roles = "admin,purchasing")]
    public async Task<IActionResult> ClosePO(string nbr)
    {
        var po = await _db.PoMstr.FindAsync(nbr);
        if (po == null) return NotFound();
        po.PoStatus = "C";
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("control")]
    public IActionResult GetPoControl([FromQuery] string site = "DEFAULT")
    {
        var ctrl = _db.PoCtrl.FirstOrDefault(c => c.PocSite == site);
        return ctrl == null ? NotFound() : Ok(ctrl);
    }

    [HttpGet("orders/{nbr}/receipts")]
    public IActionResult GetPoReceipts(string nbr)
    {
        var receipts = _db.RecvDet.Where(r => r.RvdPo == nbr).ToList();
        return Ok(receipts);
    }
}

public record CreatePoRequest(PoMstr Header, List<PodMstr> Lines, PoAddr? Addr);

// ─────────────────────────────────────────────────────────────────────────────
// SHIPPING
// Replaces: dataServSHP.java
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/shipping")]
[Authorize]
public class ShippingController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<ShippingController> _logger;

    public ShippingController(Data.ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<ShippingController> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    [HttpGet("shippers/{id}")]
    public IActionResult GetShipper(string id)
    {
        var sh = _db.ShipMstr.Find(id);
        if (sh == null) return NotFound();
        var lines = _db.ShipDet.Where(l => l.ShdId == id).OrderBy(l => l.ShdLine).ToList();
        return Ok(new { header = sh, lines });
    }

    [HttpGet("shippers")]
    public IActionResult GetShippers(
        [FromQuery] string? cust = null,
        [FromQuery] string? status = null,
        [FromQuery] string? site = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var q = _db.ShipMstr.AsQueryable();
        if (cust != null) q = q.Where(s => s.ShCust == cust);
        if (status != null) q = q.Where(s => s.ShStatus == status);
        if (site != null) q = q.Where(s => s.ShSite == site);
        var total = q.Count();
        var shippers = q.OrderByDescending(s => s.ShShipdate).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { total, page, pageSize, shippers });
    }

    [HttpPost("shippers")]
    [Authorize(Roles = "admin,shipping")]
    public async Task<IActionResult> CreateShipper([FromBody] CreateShipperRequest req)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrEmpty(req.Header.ShId))
                req.Header.ShId = await _app.GetNextDocumentNumber("SH");

            req.Header.ShEntdate = DateTime.Today.ToString("yyyy-MM-dd");
            req.Header.ShStatus = "O";
            req.Header.ShUser = User.Identity?.Name ?? string.Empty;

            int lineNum = 1;
            foreach (var line in req.Lines)
            {
                line.ShdId = req.Header.ShId;
                if (line.ShdLine == 0) line.ShdLine = lineNum++;
            }

            _db.ShipMstr.Add(req.Header);
            _db.ShipDet.AddRange(req.Lines);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { success = true, shipperId = req.Header.ShId });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error creating shipper");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("shippers/{id}/confirm")]
    [Authorize(Roles = "admin,shipping")]
    public async Task<IActionResult> ConfirmShipment(string id, [FromBody] ConfirmShipmentRequest req)
    {
        var sh = await _db.ShipMstr.FindAsync(id);
        if (sh == null) return NotFound();
        if (sh.ShPosted) return BadRequest("Shipment already confirmed.");

        sh.ShStatus = "C";
        sh.ShShipdate = req.ShipDate ?? DateTime.Today.ToString("yyyy-MM-dd");
        sh.ShTrackno = req.TrackingNumber ?? sh.ShTrackno;
        sh.ShPosted = true;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("carriers")]
    public IActionResult GetCarriers()
        => Ok(_db.CarMstr.Where(c => c.CarActive).OrderBy(c => c.CarId).ToList());

    [HttpGet("control")]
    public IActionResult GetShipControl([FromQuery] string site = "DEFAULT")
    {
        var ctrl = _db.ShipCtrl.FirstOrDefault(c => c.ShcSite == site);
        return ctrl == null ? NotFound() : Ok(ctrl);
    }
}

public record CreateShipperRequest(ShipMstr Header, List<ShipDet> Lines);
public record ConfirmShipmentRequest(string? ShipDate, string? TrackingNumber);

// ─────────────────────────────────────────────────────────────────────────────
// VENDOR
// Replaces: dataServVDR.java
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/vendors")]
[Authorize]
public class VendorController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly ILogger<VendorController> _logger;

    public VendorController(Data.ZaffreMeldDbContext db, ILogger<VendorController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public IActionResult GetVendor(string id)
    {
        var vd = _db.VdMstr.Find(id);
        return vd == null ? NotFound() : Ok(vd);
    }

    [HttpGet]
    public IActionResult SearchVendors([FromQuery] string search = "", [FromQuery] int max = 100)
    {
        var vendors = _db.VdMstr
            .Where(v => v.VdAddr.Contains(search) || v.VdName.Contains(search))
            .Where(v => v.VdStatus == "A")
            .OrderBy(v => v.VdName)
            .Take(max)
            .ToList();
        return Ok(vendors);
    }

    [HttpPost]
    [Authorize(Roles = "admin,purchasing")]
    public async Task<IActionResult> AddVendor([FromBody] VdMstr vendor)
    {
        if (_db.VdMstr.Any(v => v.VdAddr == vendor.VdAddr))
            return BadRequest(new { success = false, message = "Vendor already exists." });
        vendor.VdCrtdate = DateTime.Today.ToString("yyyy-MM-dd");
        _db.VdMstr.Add(vendor);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, vendorId = vendor.VdAddr });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,purchasing")]
    public async Task<IActionResult> UpdateVendor(string id, [FromBody] VdMstr vendor)
    {
        if (id != vendor.VdAddr) return BadRequest("ID mismatch.");
        _db.VdMstr.Update(vendor);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("{id}/pricing")]
    public IActionResult GetVendorPricing(string id, [FromQuery] string? item = null)
    {
        var q = _db.VprMstr.Where(p => p.VprVend == id && p.VprActive);
        if (item != null) q = q.Where(p => p.VprItem == item);
        return Ok(q.ToList());
    }

    [HttpGet("{id}/xref")]
    public IActionResult GetVendorXref(string id, [FromQuery] string? item = null)
    {
        var q = _db.VdpMstr.Where(x => x.VdpVend == id && x.VdpActive);
        if (item != null) q = q.Where(x => x.VdpItem == item);
        return Ok(q.ToList());
    }

    [HttpGet("{id}/shiptos")]
    public IActionResult GetVendorShipTos(string id)
        => Ok(_db.VdsDet.Where(s => s.VdsCode == id).ToList());

    [HttpGet("control")]
    public IActionResult GetVendorControl([FromQuery] string site = "DEFAULT")
    {
        var ctrl = _db.VdCtrl.FirstOrDefault(c => c.VdcSite == site);
        return ctrl == null ? NotFound() : Ok(ctrl);
    }
}
