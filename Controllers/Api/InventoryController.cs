using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers.Api;

/// <summary>
/// Inventory API controller — replaces Java dataServINV.java servlet.
/// Covers items, BOM, routing, locations, UOM, warehouses, and cost data.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _svc;
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService svc, Data.ZaffreMeldDbContext db, ILogger<InventoryController> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Items ──────────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/items/{id}</summary>
    [HttpGet("items/{id}")]
    public async Task<IActionResult> GetItem(string id)
    {
        var item = await _svc.GetItem(id);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>GET /api/inventory/items?search=</summary>
    [HttpGet("items")]
    public async Task<IActionResult> SearchItems([FromQuery] string search = "", [FromQuery] int max = 50)
    {
        var items = await _svc.SearchItems(search, max);
        return Ok(items);
    }

    /// <summary>POST /api/inventory/items</summary>
    [HttpPost("items")]
    [Authorize(Roles = "admin,inventory")]
    public async Task<IActionResult> AddItem([FromBody] ItemMstr item)
    {
        var result = await _svc.AddItem(item);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/inventory/items/{id}</summary>
    [HttpPut("items/{id}")]
    [Authorize(Roles = "admin,inventory")]
    public async Task<IActionResult> UpdateItem(string id, [FromBody] ItemMstr item)
    {
        if (id != item.ItItem) return BadRequest("ID mismatch.");
        var result = await _svc.UpdateItem(item);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/inventory/items/{id}</summary>
    [HttpDelete("items/{id}")]
    [Authorize(Roles = "admin,inventory")]
    public async Task<IActionResult> DeleteItem(string id)
    {
        var result = await _svc.DeleteItem(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Item Cost ──────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/items/{id}/cost?site=&set=STD</summary>
    [HttpGet("items/{id}/cost")]
    public async Task<IActionResult> GetItemCost(string id, [FromQuery] string site, [FromQuery] string set = "STD")
    {
        var cost = await _svc.GetItemCost(id, site, set);
        return cost == null ? NotFound() : Ok(cost);
    }

    /// <summary>POST /api/inventory/items/{id}/cost</summary>
    [HttpPost("items/{id}/cost")]
    [Authorize(Roles = "admin,inventory")]
    public async Task<IActionResult> SetItemCost(string id, [FromBody] ItemCost cost)
    {
        var existing = await _svc.GetItemCost(cost.ItcItem, cost.ItcSite, cost.ItcSet);
        if (existing == null)
        {
            cost.ItcTotalcost = cost.ItcMatcost + cost.ItcLabcost + cost.ItcOvhcost + cost.ItcBurdcost;
            cost.ItcEffdate   = DateTime.Today.ToString("yyyy-MM-dd");
            _db.ItemCost.Add(cost);
        }
        else
        {
            existing.ItcMatcost = cost.ItcMatcost;
            existing.ItcLabcost = cost.ItcLabcost;
            existing.ItcOvhcost = cost.ItcOvhcost;
            existing.ItcBurdcost = cost.ItcBurdcost;
            existing.ItcTotalcost = cost.ItcMatcost + cost.ItcLabcost + cost.ItcOvhcost + cost.ItcBurdcost;
            existing.ItcEffdate = DateTime.Today.ToString("yyyy-MM-dd");
        }
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ── BOM ────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/bom/{id}</summary>
    [HttpGet("bom/{id}")]
    public async Task<IActionResult> GetBom(string id)
    {
        var bom = await _db.BomMstr.FindAsync(id);
        return bom == null ? NotFound() : Ok(bom);
    }

    /// <summary>GET /api/inventory/bom/{id}/structure — returns parent/child tree</summary>
    [HttpGet("bom/{id}/structure")]
    public IActionResult GetBomStructure(string id)
    {
        var components = _db.PbmMstr
            .Where(p => p.PsParent == id && p.PsActive)
            .OrderBy(p => p.PsSeq)
            .ToList();
        return Ok(components);
    }

    /// <summary>GET /api/inventory/items/{id}/where-used — WhereUsedUtility</summary>
    [HttpGet("items/{id}/where-used")]
    public IActionResult GetWhereUsed(string id)
    {
        var parents = _db.PbmMstr
            .Where(p => p.PsChild == id && p.PsActive)
            .OrderBy(p => p.PsParent)
            .ToList();
        return Ok(parents);
    }

    // ── Work Centers & Routing ─────────────────────────────────────────────────

    /// <summary>GET /api/inventory/workcenters</summary>
    [HttpGet("workcenters")]
    public IActionResult GetWorkCenters([FromQuery] string? site = null)
    {
        var q = _db.WcMstr.Where(w => w.WcActive);
        if (site != null) q = q.Where(w => w.WcSite == site);
        return Ok(q.OrderBy(w => w.WcCell).ToList());
    }

    /// <summary>GET /api/inventory/workcenters/{id}</summary>
    [HttpGet("workcenters/{id}")]
    public async Task<IActionResult> GetWorkCenter(string id)
    {
        var wc = await _db.WcMstr.FindAsync(id);
        return wc == null ? NotFound() : Ok(wc);
    }

    // ── Locations ──────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/locations?wh=</summary>
    [HttpGet("locations")]
    public IActionResult GetLocations([FromQuery] string? wh = null, [FromQuery] string? site = null)
    {
        var q = _db.LocMstr.Where(l => l.LocActive);
        if (wh != null) q = q.Where(l => l.LocWh == wh);
        if (site != null) q = q.Where(l => l.LocSite == site);
        return Ok(q.OrderBy(l => l.LocLoc).ToList());
    }

    // ── Warehouses ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/warehouses</summary>
    [HttpGet("warehouses")]
    public IActionResult GetWarehouses([FromQuery] string? site = null)
    {
        var q = _db.WhMstr.Where(w => w.WhActive);
        if (site != null) q = q.Where(w => w.WhSite == site);
        return Ok(q.OrderBy(w => w.WhId).ToList());
    }

    // ── UOM ────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/uom</summary>
    [HttpGet("uom")]
    public IActionResult GetUom()
        => Ok(_db.UomMstr.Where(u => u.UomActive).OrderBy(u => u.UomId).ToList());

    /// <summary>GET /api/inventory/uom/{id}/convert?toUom=&qty=</summary>
    [HttpGet("uom/{id}/convert")]
    public async Task<IActionResult> ConvertUom(string id, [FromQuery] string toUom, [FromQuery] decimal qty)
    {
        var fromUom = await _db.UomMstr.FindAsync(id);
        var targetUom = await _db.UomMstr.FindAsync(toUom);
        if (fromUom == null || targetUom == null) return NotFound();

        var convertedQty = qty * fromUom.UomConvFactor / targetUom.UomConvFactor;
        return Ok(new { fromUom = id, toUom, originalQty = qty, convertedQty });
    }

    // ── QOH ────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/items/{id}/qoh?site=</summary>
    [HttpGet("items/{id}/qoh")]
    public async Task<IActionResult> GetQoh(string id, [FromQuery] string site)
    {
        var qoh = await _svc.GetItemQoh(id, site);
        return Ok(new { item = id, site, qoh });
    }

    // ── Item Browse ────────────────────────────────────────────────────────────

    /// <summary>GET /api/inventory/browse?type=&status=&group= — item browse list</summary>
    [HttpGet("browse")]
    public IActionResult BrowseItems(
        [FromQuery] string? type = null,
        [FromQuery] string? status = "A",
        [FromQuery] string? group = null,
        [FromQuery] string? site = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var q = _db.ItemMstr.AsQueryable();
        if (type != null) q = q.Where(i => i.ItType == type);
        if (status != null) q = q.Where(i => i.ItStatus == status);
        if (group != null) q = q.Where(i => i.ItGroup == group);
        if (site != null) q = q.Where(i => i.ItSite == site);

        var total = q.Count();
        var items = q.OrderBy(i => i.ItItem)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize)
                     .Select(i => new { i.ItItem, i.ItDesc, i.ItType, i.ItStatus, i.ItUom, i.ItQoh })
                     .ToList();

        return Ok(new { total, page, pageSize, items });
    }
}