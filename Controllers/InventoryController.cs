using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("inventory")]
public class InventoryController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public InventoryController(ZaffreMeldDbContext db) => _db = db;

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] string? q, [FromQuery] string? type, [FromQuery] string? status)
    {
        var query = _db.ItemMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.ItStatus == status);
        if (!string.IsNullOrEmpty(type))   query = query.Where(i => i.ItType == type);
        if (!string.IsNullOrEmpty(q))      query = query.Where(i => i.ItItem.Contains(q) || i.ItDesc.Contains(q));
        ViewBag.Items = await query.OrderBy(i => i.ItItem).Take(200).ToListAsync();
        ViewBag.Q = q; ViewBag.Type = type;
        return View();
    }

    [HttpGet("items/{id}")]
    public async Task<IActionResult> Item(string id)
    {
        var item = await _db.ItemMstr.FindAsync(id);
        if (item == null) return NotFound();
        ViewBag.BomLines = await _db.PbmMstr.Where(b => b.PsParent == id).OrderBy(b => b.PsSeq).ToListAsync();
        ViewBag.Cost = await _db.ItemCost.FindAsync(id);
        return View(item);
    }

    [HttpGet("items/new")]
    public IActionResult NewItem() => View(new ItemMstr { ItStatus = "A", ItType = "M", ItUom = "EA" });

    [HttpPost("items/new")]
    public async Task<IActionResult> NewItem([FromForm] ItemMstr item)
    {
        // Coalesce nulls — optional string fields left blank by model binding
        item.ItDesc     ??= string.Empty;
        item.ItBom      ??= string.Empty;
        item.ItRoute    ??= string.Empty;
        item.ItProdline ??= string.Empty;
        item.ItGroup    ??= string.Empty;
        item.ItWh       ??= string.Empty;
        item.ItLoc      ??= string.Empty;
        item.ItDrawing  ??= string.Empty;
        item.ItRevision ??= string.Empty;
        item.ItSite     ??= string.Empty;
        item.ItUom      ??= "EA";
        item.ItType     ??= "M";
        item.ItStatus   ??= "A";
        item.ItAbc      ??= "C";
        _db.ItemMstr.Add(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Item), new { id = item.ItItem });
    }

    [HttpPost("items/{id}/edit")]
    public async Task<IActionResult> EditItem(string id, [FromForm] ItemMstr item)
    {
        item.ItItem = id;
        _db.ItemMstr.Update(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Item), new { id });
    }

    [HttpGet("bom")]
    public async Task<IActionResult> Bom([FromQuery] string? parent)
    {
        if (!string.IsNullOrEmpty(parent))
        {
            ViewBag.ParentItem = await _db.ItemMstr.FindAsync(parent);
            ViewBag.Lines = await _db.PbmMstr.Where(b => b.PsParent == parent).OrderBy(b => b.PsSeq).ToListAsync();
        }
        ViewBag.Items = await _db.ItemMstr.Where(i => i.ItStatus == "A").OrderBy(i => i.ItItem)
            .Select(i => new { i.ItItem, i.ItDesc }).ToListAsync();
        return View();
    }

    [HttpGet("locations")]
    public async Task<IActionResult> Locations([FromQuery] string? wh)
    {
        ViewBag.Warehouses = await _db.WhMstr.OrderBy(w => w.WhId).ToListAsync();
        var locQuery = _db.LocMstr.AsQueryable();
        if (!string.IsNullOrEmpty(wh)) locQuery = locQuery.Where(l => l.LocWh == wh);
        ViewBag.Locations = await locQuery.OrderBy(l => l.LocLoc).Take(300).ToListAsync();
        ViewBag.SelectedWh = wh;
        return View();
    }
}
