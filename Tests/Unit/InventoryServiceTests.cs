using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Tests.Unit;

public class InventoryServiceTests : IDisposable
{
    private readonly ZaffreMeld.Web.Data.ZaffreMeldDbContext _db;
    private readonly InventoryService _svc;

    public InventoryServiceTests()
    {
        _db  = TestDbFactory.CreateSeeded();
        _svc = new InventoryService(_db, NullLogger<InventoryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── GetItem ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItem_ExistingItem_ReturnsItem()
    {
        var item = await _svc.GetItem("WIDGET-100");

        item.Should().NotBeNull();
        item!.ItDesc.Should().Be("Blue Widget");
        item.ItStatus.Should().Be("A");
    }

    [Fact]
    public async Task GetItem_NonExistent_ReturnsNull()
    {
        var item = await _svc.GetItem("DOES-NOT-EXIST");
        item.Should().BeNull();
    }

    // ── SearchItems ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchItems_ByItemCode_ReturnsMatch()
    {
        var results = await _svc.SearchItems("WIDGET");

        results.Should().HaveCount(1);
        results.Single().ItItem.Should().Be("WIDGET-100");
    }

    [Fact]
    public async Task SearchItems_ByDescription_ReturnsMatch()
    {
        var results = await _svc.SearchItems("Gadget");

        results.Should().HaveCount(1);
        results.Single().ItItem.Should().Be("GADGET-200");
    }

    [Fact]
    public async Task SearchItems_CaseInsensitive_FindsItem()
    {
        var results = await _svc.SearchItems("widget");
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchItems_NoMatch_ReturnsEmpty()
    {
        var results = await _svc.SearchItems("ZZNOTFOUND");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchItems_RespectsMaxResults()
    {
        // Add 10 more items
        for (int i = 1; i <= 10; i++)
            _db.ItemMstr.Add(new ItemMstr { ItItem = $"ITEM-{i:D3}", ItDesc = "Generic", ItSite = "DEFAULT", ItStatus = "A" });
        await _db.SaveChangesAsync();

        var results = await _svc.SearchItems("ITEM", maxResults: 3);
        results.Should().HaveCount(3);
    }

    // ── AddItem ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_NewItem_Succeeds()
    {
        var item = new ItemMstr
        {
            ItItem = "NEW-PART-001",
            ItDesc = "Brand New Part",
            ItSite = "DEFAULT",
            ItType = "P",
            ItStatus = "A"
        };

        var result = await _svc.AddItem(item);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("added");

        var saved = await _svc.GetItem("NEW-PART-001");
        saved.Should().NotBeNull();
        saved!.ItCrtdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task AddItem_SetsCreatedDateToToday()
    {
        var item = new ItemMstr { ItItem = "DATE-TEST", ItDesc = "Date Check", ItSite = "DEFAULT", ItStatus = "A" };
        await _svc.AddItem(item);

        var saved = await _svc.GetItem("DATE-TEST");
        saved!.ItCrtdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task AddItem_DuplicateItemCode_ReturnsError()
    {
        var dup = new ItemMstr { ItItem = "WIDGET-100", ItDesc = "Duplicate", ItSite = "DEFAULT", ItStatus = "A" };

        var result = await _svc.AddItem(dup);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    // ── UpdateItem ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateItem_ChangesDescription()
    {
        var item = await _svc.GetItem("WIDGET-100");
        item!.ItDesc = "Updated Blue Widget";

        var result = await _svc.UpdateItem(item);

        result.Success.Should().BeTrue();
        (await _svc.GetItem("WIDGET-100"))!.ItDesc.Should().Be("Updated Blue Widget");
    }

    [Fact]
    public async Task UpdateItem_ChangesStatus()
    {
        var item = await _svc.GetItem("WIDGET-100");
        item!.ItStatus = "I";

        await _svc.UpdateItem(item);

        (await _svc.GetItem("WIDGET-100"))!.ItStatus.Should().Be("I");
    }

    // ── DeleteItem ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteItem_ExistingItem_RemovesIt()
    {
        var result = await _svc.DeleteItem("OBSOLETE");

        result.Success.Should().BeTrue();
        (await _svc.GetItem("OBSOLETE")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteItem_NonExistent_ReturnsError()
    {
        var result = await _svc.DeleteItem("NEVER-EXISTED");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    // ── GetItemCost ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemCost_ExistingCost_ReturnsCost()
    {
        var cost = await _svc.GetItemCost("WIDGET-100", "DEFAULT", "STD");

        cost.Should().NotBeNull();
        cost!.ItcTotalcost.Should().Be(12.50m);
    }

    [Fact]
    public async Task GetItemCost_WrongSite_ReturnsNull()
    {
        var cost = await _svc.GetItemCost("WIDGET-100", "SITE-X", "STD");
        cost.Should().BeNull();
    }

    [Fact]
    public async Task GetItemCost_DefaultsToStdCostSet()
    {
        var cost = await _svc.GetItemCost("GADGET-200", "DEFAULT");
        cost!.ItcTotalcost.Should().Be(35.00m);
    }

    // ── GetItemQoh ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemQoh_ReturnsCorrectQty()
    {
        var qoh = await _svc.GetItemQoh("WIDGET-100", "DEFAULT");
        qoh.Should().Be(100m);
    }

    [Fact]
    public async Task GetItemQoh_WrongSite_ReturnsZero()
    {
        var qoh = await _svc.GetItemQoh("WIDGET-100", "WRONG-SITE");
        qoh.Should().Be(0m);
    }

    [Fact]
    public async Task GetItemQoh_NonExistentItem_ReturnsZero()
    {
        var qoh = await _svc.GetItemQoh("GHOST-ITEM", "DEFAULT");
        qoh.Should().Be(0m);
    }
}
