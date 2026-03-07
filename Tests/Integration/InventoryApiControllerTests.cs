using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

public class InventoryApiControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IInventoryService> _svcMock;
    private readonly InventoryController _ctrl;

    public InventoryApiControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _svcMock = new Mock<IInventoryService>();

        // Default pass-throughs from in-memory db
        _svcMock.Setup(s => s.GetItem(It.IsAny<string>()))
            .ReturnsAsync((string id) => _db.ItemMstr.Find(id));
        _svcMock.Setup(s => s.SearchItems(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string q, int max) => _db.ItemMstr
                .Where(i => i.ItItem.Contains(q) || i.ItDesc.Contains(q))
                .OrderBy(i => i.ItItem).Take(max).ToList());
        _svcMock.Setup(s => s.AddItem(It.IsAny<ItemMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Item added successfully."));
        _svcMock.Setup(s => s.UpdateItem(It.IsAny<ItemMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Item updated."));
        _svcMock.Setup(s => s.DeleteItem(It.IsAny<string>()))
            .ReturnsAsync(ServiceResult.Ok("Item deleted."));
        _svcMock.Setup(s => s.GetItemCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string item, string site, string set) => _db.ItemCost.Find(item, site, set));
        _svcMock.Setup(s => s.GetItemQoh(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string item, string site) => _db.ItemMstr
                .Where(i => i.ItItem == item && i.ItSite == site)
                .Select(i => i.ItQoh).FirstOrDefault());

        // Seed warehouses, work centers, locations
        _db.WhMstr.AddRange(
            new WhMstr { WhId = "WH-01", WhSite = "DEFAULT", WhDesc = "Main Warehouse", WhActive = true },
            new WhMstr { WhId = "WH-02", WhSite = "DEFAULT", WhDesc = "Cold Storage",   WhActive = true },
            new WhMstr { WhId = "WH-OLD", WhSite = "DEFAULT", WhDesc = "Decommissioned", WhActive = false }
        );
        _db.WcMstr.AddRange(
            new WcMstr { WcCell = "CELL-A", WcDesc = "Assembly",  WcSite = "DEFAULT", WcActive = true },
            new WcMstr { WcCell = "CELL-B", WcDesc = "Packaging",  WcSite = "DEFAULT", WcActive = true },
            new WcMstr { WcCell = "CELL-OFF", WcDesc = "Offline",  WcSite = "DEFAULT", WcActive = false }
        );
        _db.LocMstr.AddRange(
            new LocMstr { LocLoc = "A-01-01", LocSite = "DEFAULT", LocWh = "WH-01", LocActive = true },
            new LocMstr { LocLoc = "A-01-02", LocSite = "DEFAULT", LocWh = "WH-01", LocActive = true },
            new LocMstr { LocLoc = "B-01-01", LocSite = "DEFAULT", LocWh = "WH-02", LocActive = true }
        );
        _db.UomMstr.AddRange(
            new UomMstr { UomId = "EA", UomDesc = "Each",  UomConvFactor = 1m,   UomActive = true },
            new UomMstr { UomId = "DZ", UomDesc = "Dozen", UomConvFactor = 12m,  UomActive = true },
            new UomMstr { UomId = "CS", UomDesc = "Case",  UomConvFactor = 24m,  UomActive = true }
        );
        _db.BomMstr.Add(new BomMstr { BomId = "GADGET-200", BomDesc = "Gadget BOM", BomSite = "DEFAULT", BomRevision = "A", BomStatus = "A" });
        _db.PbmMstr.AddRange(
            new PbmMstr { PsParent = "GADGET-200", PsChild = "WIDGET-100", PsQty = 2m, PsSeq = 10, PsActive = true },
            new PbmMstr { PsParent = "GADGET-200", PsChild = "OBSOLETE",   PsQty = 1m, PsSeq = 20, PsActive = true }
        );
        _db.SaveChanges();

        _ctrl = new InventoryController(_svcMock.Object, _db, NullLogger<InventoryController>.Instance);
        SetUser(_ctrl, "invuser");
    }

    public void Dispose() => _db.Dispose();

    // ── Items ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItem_ExistingItem_Returns200()
    {
        (await _ctrl.GetItem("WIDGET-100")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetItem_NonExistent_Returns404()
    {
        _svcMock.Setup(s => s.GetItem("GHOST")).ReturnsAsync((ItemMstr?)null);
        (await _ctrl.GetItem("GHOST")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SearchItems_ReturnsMatchingItems()
    {
        var result = await _ctrl.SearchItems("WIDGET") as OkObjectResult;
        ((List<ItemMstr>)result.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task AddItem_Success_Returns200()
    {
        (await _ctrl.AddItem(new ItemMstr { ItItem = "NEW-ITEM", ItDesc = "New", ItSite = "DEFAULT", ItStatus = "A" }))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddItem_Failure_Returns400()
    {
        _svcMock.Setup(s => s.AddItem(It.IsAny<ItemMstr>()))
            .ReturnsAsync(ServiceResult.Error("Item already exists."));
        (await _ctrl.AddItem(new ItemMstr { ItItem = "WIDGET-100" })).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateItem_IdMismatch_Returns400()
    {
        var item = new ItemMstr { ItItem = "MISMATCH" };
        (await _ctrl.UpdateItem("WIDGET-100", item)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateItem_Success_Returns200()
    {
        var item = new ItemMstr { ItItem = "WIDGET-100", ItDesc = "Updated", ItSite = "DEFAULT", ItStatus = "A" };
        (await _ctrl.UpdateItem("WIDGET-100", item)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteItem_Success_Returns200()
    {
        (await _ctrl.DeleteItem("WIDGET-100")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteItem_Failure_Returns400()
    {
        _svcMock.Setup(s => s.DeleteItem("GHOST")).ReturnsAsync(ServiceResult.Error("Item not found."));
        (await _ctrl.DeleteItem("GHOST")).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Item Cost ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemCost_ExistingCost_Returns200()
    {
        (await _ctrl.GetItemCost("WIDGET-100", "DEFAULT", "STD")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetItemCost_NonExistent_Returns404()
    {
        _svcMock.Setup(s => s.GetItemCost("GHOST", "DEFAULT", "STD")).ReturnsAsync((ItemCost?)null);
        (await _ctrl.GetItemCost("GHOST", "DEFAULT", "STD")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetItemCost_NewCost_AddsCostRecord()
    {
        // Item not in ItemCost yet
        var cost = new ItemCost { ItcItem = "OBSOLETE", ItcSite = "DEFAULT", ItcSet = "STD", ItcMatcost = 5m, ItcLabcost = 2m, ItcOvhcost = 1m, ItcBurdcost = 0.5m };

        await _ctrl.SetItemCost("OBSOLETE", cost);

        var saved = _db.ItemCost.Find("OBSOLETE", "DEFAULT", "STD");
        saved.Should().NotBeNull();
        saved!.ItcTotalcost.Should().Be(8.5m); // 5+2+1+0.5
    }

    [Fact]
    public async Task SetItemCost_ExistingCost_UpdatesInPlace()
    {
        // WIDGET-100 already has STD cost
        var cost = new ItemCost { ItcItem = "WIDGET-100", ItcSite = "DEFAULT", ItcSet = "STD", ItcMatcost = 8m, ItcLabcost = 2m, ItcOvhcost = 1m, ItcBurdcost = 0m };

        await _ctrl.SetItemCost("WIDGET-100", cost);

        var updated = _db.ItemCost.Find("WIDGET-100", "DEFAULT", "STD");
        updated!.ItcTotalcost.Should().Be(11m); // 8+2+1+0
        updated.ItcEffdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task SetItemCost_TotalIsCalculated_FromComponents()
    {
        var cost = new ItemCost
        {
            ItcItem = "NEW-COST-ITEM", ItcSite = "DEFAULT", ItcSet = "STD",
            ItcMatcost  = 10m,
            ItcLabcost  = 3m,
            ItcOvhcost  = 2m,
            ItcBurdcost = 1.5m
        };

        await _ctrl.SetItemCost("NEW-COST-ITEM", cost);

        var saved = _db.ItemCost.Find("NEW-COST-ITEM", "DEFAULT", "STD");
        saved!.ItcTotalcost.Should().Be(16.5m);
    }

    // ── QOH ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQoh_ReturnsQohAndMetadata()
    {
        var result = await _ctrl.GetQoh("WIDGET-100", "DEFAULT") as OkObjectResult;
        var body   = result!.Value;

        Anon.Prop<string>(body, "item").Should().Be("WIDGET-100");
        Anon.Prop<string>(body, "site").Should().Be("DEFAULT");
        Anon.Prop<decimal>(body, "qoh").Should().Be(100m);
    }

    [Fact]
    public async Task GetQoh_ZeroForUnknownItem()
    {
        _svcMock.Setup(s => s.GetItemQoh("GHOST", "DEFAULT")).ReturnsAsync(0m);
        var result = await _ctrl.GetQoh("GHOST", "DEFAULT") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<decimal>(body, "qoh").Should().Be(0m);
    }

    // ── BOM ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBom_ExistingBom_Returns200()
    {
        (await _ctrl.GetBom("GADGET-200")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBom_NonExistent_Returns404()
    {
        (await _ctrl.GetBom("GHOST-BOM")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetBomStructure_ReturnsComponents_OrderedBySeq()
    {
        var result = _ctrl.GetBomStructure("GADGET-200") as OkObjectResult;
        var comps  = ((List<PbmMstr>)result.Value!);
        comps.Should().HaveCount(2);
        comps[0].PsSeq.Should().Be(10);
        comps[1].PsSeq.Should().Be(20);
    }

    [Fact]
    public void GetBomStructure_ExcludesInactiveComponents()
    {
        _db.PbmMstr.First(p => p.PsChild == "OBSOLETE").PsActive = false;
        _db.SaveChanges();

        var result = _ctrl.GetBomStructure("GADGET-200") as OkObjectResult;
        ((List<PbmMstr>)result.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetBomStructure_NoComponents_ReturnsEmpty()
    {
        var result = _ctrl.GetBomStructure("WIDGET-100") as OkObjectResult;
        ((List<PbmMstr>)result.Value!).Should().BeEmpty();
    }

    [Fact]
    public void GetWhereUsed_ReturnsParentsUsingItem()
    {
        // WIDGET-100 is used in GADGET-200
        var result = _ctrl.GetWhereUsed("WIDGET-100") as OkObjectResult;
        var parents = ((List<PbmMstr>)result.Value!);
        parents.Should().HaveCount(1);
        parents.Single().PsParent.Should().Be("GADGET-200");
    }

    [Fact]
    public void GetWhereUsed_NotUsedAnywhere_ReturnsEmpty()
    {
        var result = _ctrl.GetWhereUsed("GADGET-200") as OkObjectResult;
        ((List<PbmMstr>)result.Value!).Should().BeEmpty();
    }

    // ── Work Centers ───────────────────────────────────────────────────────────

    [Fact]
    public void GetWorkCenters_ReturnsActiveOnly()
    {
        var result = _ctrl.GetWorkCenters() as OkObjectResult;
        var wcs    = ((List<WcMstr>)result.Value!);
        wcs.Should().HaveCount(2);
        wcs.Should().NotContain(w => w.WcCell == "CELL-OFF");
    }

    [Fact]
    public void GetWorkCenters_FiltersBySite()
    {
        _db.WcMstr.Add(new WcMstr { WcCell = "WEST-A", WcDesc = "West Cell", WcSite = "WEST", WcActive = true });
        _db.SaveChanges();

        var result = _ctrl.GetWorkCenters(site: "WEST") as OkObjectResult;
        ((List<WcMstr>)result.Value!).Should().OnlyContain(w => w.WcSite == "WEST");
    }

    [Fact]
    public async Task GetWorkCenter_ExistingId_Returns200()
    {
        (await _ctrl.GetWorkCenter("CELL-A")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetWorkCenter_NonExistent_Returns404()
    {
        (await _ctrl.GetWorkCenter("GHOST-CELL")).Should().BeOfType<NotFoundResult>();
    }

    // ── Warehouses ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetWarehouses_ReturnsActiveOnly()
    {
        var result = _ctrl.GetWarehouses() as OkObjectResult;
        var whs    = ((List<WhMstr>)result.Value!);
        whs.Should().HaveCount(2);
        whs.Should().NotContain(w => w.WhId == "WH-OLD");
    }

    [Fact]
    public void GetWarehouses_FiltersBySite()
    {
        _db.WhMstr.Add(new WhMstr { WhId = "WH-WEST", WhSite = "WEST", WhDesc = "West WH", WhActive = true });
        _db.SaveChanges();

        var result = _ctrl.GetWarehouses(site: "DEFAULT") as OkObjectResult;
        ((List<WhMstr>)result.Value!).Should().OnlyContain(w => w.WhSite == "DEFAULT");
    }

    // ── Locations ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetLocations_ReturnsActiveOnly()
    {
        _db.LocMstr.Add(new LocMstr { LocLoc = "DEAD", LocSite = "DEFAULT", LocWh = "WH-01", LocActive = false });
        _db.SaveChanges();

        var result = _ctrl.GetLocations() as OkObjectResult;
        ((List<LocMstr>)result.Value!).Should().NotContain(l => l.LocLoc == "DEAD");
    }

    [Fact]
    public void GetLocations_FiltersByWarehouse()
    {
        var result = _ctrl.GetLocations(wh: "WH-01") as OkObjectResult;
        ((List<LocMstr>)result.Value!).Should().OnlyContain(l => l.LocWh == "WH-01");
    }

    [Fact]
    public void GetLocations_FiltersBySite()
    {
        var result = _ctrl.GetLocations(site: "DEFAULT") as OkObjectResult;
        ((List<LocMstr>)result.Value!).Should().OnlyContain(l => l.LocSite == "DEFAULT");
    }

    // ── UOM ────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetUom_ReturnsActiveUoms()
    {
        _db.UomMstr.Add(new UomMstr { UomId = "BX", UomDesc = "Box", UomConvFactor = 6m, UomActive = false });
        _db.SaveChanges();

        var result = _ctrl.GetUom() as OkObjectResult;
        ((List<UomMstr>)result.Value!).Should().NotContain(u => u.UomId == "BX");
    }

    [Fact]
    public async Task ConvertUom_EaToDz_ConvertsCorrectly()
    {
        // 24 EA ÷ 1 = 24; then ÷ 12 (DZ factor) = 2 dozen
        var result = await _ctrl.ConvertUom("EA", "DZ", 24m) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<decimal>(body, "convertedQty").Should().BeApproximately(2m, 0.001m);
    }

    [Fact]
    public async Task ConvertUom_EaToCs_ConvertsCorrectly()
    {
        // 48 EA → cases of 24 = 2 cases
        var result = await _ctrl.ConvertUom("EA", "CS", 48m) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<decimal>(body, "convertedQty").Should().BeApproximately(2m, 0.001m);
    }

    [Fact]
    public async Task ConvertUom_SameUom_ReturnsSameQty()
    {
        var result = await _ctrl.ConvertUom("EA", "EA", 10m) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<decimal>(body, "convertedQty").Should().Be(10m);
    }

    [Fact]
    public async Task ConvertUom_UnknownFromUom_Returns404()
    {
        (await _ctrl.ConvertUom("GHOST", "EA", 1m)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ConvertUom_UnknownToUom_Returns404()
    {
        (await _ctrl.ConvertUom("EA", "GHOST", 1m)).Should().BeOfType<NotFoundResult>();
    }

    // ── Browse ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BrowseItems_DefaultsToActiveStatus()
    {
        var result = _ctrl.BrowseItems() as OkObjectResult;
        var body   = result!.Value;
        // Seeded: 2 active items, 1 inactive
        Anon.Prop<int>(body, "total").Should().Be(2);
    }

    [Fact]
    public void BrowseItems_FiltersByType()
    {
        // All seeded items are type "P" (Purchase)
        var result = _ctrl.BrowseItems(type: "P", status: null) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(3);
    }

    [Fact]
    public void BrowseItems_NullStatus_ReturnsAll()
    {
        var result = _ctrl.BrowseItems(status: null) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(3);
    }

    [Fact]
    public void BrowseItems_Pagination_RespectsPageSize()
    {
        var result = _ctrl.BrowseItems(status: null, pageSize: 2) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(3);

        // page contains only 2
        var items = Anon.Prop<System.Collections.IList>(body, "items");
        items.Count.Should().Be(2);
    }

    [Fact]
    public void BrowseItems_Page2_ReturnsRemainingItem()
    {
        var result = _ctrl.BrowseItems(status: null, page: 2, pageSize: 2) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<System.Collections.IList>(body, "items").Count.Should().Be(1);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void SetUser(ControllerBase ctrl, string username)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, username) }, "test"))
            }
        };
    }
}