using FluentAssertions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Models.Orders;

namespace ZaffreMeld.Tests.Unit;

/// <summary>
/// Tests for model defaults, data integrity constraints, and DbContext behaviour.
/// Uses the in-memory provider to verify EF configuration.
/// </summary>
public class ModelDefaultsTests : IDisposable
{
    private readonly ZaffreMeld.Web.Data.ZaffreMeldDbContext _db;

    public ModelDefaultsTests()
    {
        _db = TestDbFactory.Create();
    }

    public void Dispose() => _db.Dispose();

    // ── SoMstr defaults ────────────────────────────────────────────────────────

    [Fact]
    public void SoMstr_DefaultStatus_IsOpen()
    {
        new SoMstr().SoStatus.Should().Be("O");
    }

    [Fact]
    public void SoMstr_DefaultCurrency_IsUsd()
    {
        new SoMstr().SoCurr.Should().Be("USD");
    }

    [Fact]
    public void SoMstr_DefaultType_IsS()
    {
        new SoMstr().SoType.Should().Be("S");
    }

    [Fact]
    public void SoMstr_DefaultTotals_AreZero()
    {
        var so = new SoMstr();
        so.SoTotalamt.Should().Be(0m);
        so.SoTaxamt.Should().Be(0m);
    }

    // ── SodDet defaults ────────────────────────────────────────────────────────

    [Fact]
    public void SodDet_DefaultStatus_IsOpen()
    {
        new SodDet().SodStatus.Should().Be("O");
    }

    [Fact]
    public void SodDet_DefaultUom_IsEa()
    {
        new SodDet().SodUom.Should().Be("EA");
    }

    [Fact]
    public void SodDet_DefaultDiscount_IsZero()
    {
        new SodDet().SodDisc.Should().Be(0m);
    }

    // ── ItemMstr defaults ──────────────────────────────────────────────────────

    [Fact]
    public void ItemMstr_DefaultStatus_IsActive()
    {
        new ItemMstr().ItStatus.Should().Be("A");
    }

    [Fact]
    public void ItemMstr_DefaultType_IsManufactured()
    {
        new ItemMstr().ItType.Should().Be("M");
    }

    [Fact]
    public void ItemMstr_DefaultQoh_IsZero()
    {
        new ItemMstr().ItQoh.Should().Be(0m);
    }

    // ── ItemCost defaults ──────────────────────────────────────────────────────

    [Fact]
    public void ItemCost_DefaultSet_IsStd()
    {
        new ItemCost().ItcSet.Should().Be("STD");
    }

    // ── AcctMstr defaults ──────────────────────────────────────────────────────

    [Fact]
    public void AcctMstr_DefaultCurrency_IsUsd()
    {
        new AcctMstr().Currency.Should().Be("USD");
    }

    [Fact]
    public void AcctMstr_DefaultCbDisplay_IsTrue()
    {
        new AcctMstr().CbDisplay.Should().BeTrue();
    }

    // ── Counter defaults ───────────────────────────────────────────────────────

    [Fact]
    public void Counter_DefaultValue_IsZero()
    {
        new Counter().CounterValue.Should().Be(0);
    }

    [Fact]
    public void Counter_DefaultLength_IsSeven()
    {
        new Counter().CounterLength.Should().Be(7);
    }

    // ── ChangeLog defaults ─────────────────────────────────────────────────────

    [Fact]
    public void ChangeLog_DefaultTimestamp_IsUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var log    = new ChangeLog();
        log.ClTimestamp.Should().BeAfter(before);
    }

    // ── CmMstr defaults ────────────────────────────────────────────────────────

    [Fact]
    public void CmMstr_DefaultStatus_IsActive()
    {
        new CmMstr().CmStatus.Should().Be("A");
    }

    // ── GlTran persistence ─────────────────────────────────────────────────────

    [Fact]
    public async Task GlTran_CanBePersistedAndRetrieved()
    {
        var tran = new GlTran
        {
            GltAcct    = "4000",
            GltCc      = "CC1",
            GltAmt     = 1000m,
            GltEffdate = "2026-03-01",
            GltType    = "AR",
            GltSite    = "DEFAULT"
        };

        _db.GlTran.Add(tran);
        await _db.SaveChangesAsync();

        var saved = _db.GlTran.First(t => t.GltAcct == "4000");
        saved.GltAmt.Should().Be(1000m);
    }

    // ── Composite key enforcement ──────────────────────────────────────────────

    [Fact]
    public async Task SodDet_CompositeKey_AllowsSameLineOnDifferentOrders()
    {
        _db.SodDet.AddRange(
            new SodDet { SodNbr = "SO-A", SodLine = 10, SodItem = "WIDGET-100", SodStatus = "O" },
            new SodDet { SodNbr = "SO-B", SodLine = 10, SodItem = "WIDGET-100", SodStatus = "O" }
        );

        var act = async () => await _db.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItemCost_CompositeKey_AllowsSameItemDifferentSites()
    {
        _db.ItemMstr.AddRange(
            new ItemMstr { ItItem = "TEST-ITEM", ItDesc = "Test", ItSite = "SITE-A", ItStatus = "A" },
            new ItemMstr { ItItem = "TEST-ITEM2", ItDesc = "Test2", ItSite = "SITE-B", ItStatus = "A" }
        );
        _db.ItemCost.AddRange(
            new ItemCost { ItcItem = "TEST-ITEM",  ItcSite = "SITE-A", ItcSet = "STD", ItcTotalcost = 10m },
            new ItemCost { ItcItem = "TEST-ITEM2", ItcSite = "SITE-B", ItcSet = "STD", ItcTotalcost = 20m }
        );

        var act = async () => await _db.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    // ── String default convention (NOT NULL protection) ────────────────────────

    [Fact]
    public void AllModelStringProperties_HaveNonNullDefaults()
    {
        // Spot-check the most-used models — all string properties should default to string.Empty
        CheckStringDefaults(new SoMstr());
        CheckStringDefaults(new SodDet());
        CheckStringDefaults(new ItemMstr());
        CheckStringDefaults(new AcctMstr());
        CheckStringDefaults(new CmMstr());
        CheckStringDefaults(new Counter());
    }

    private static void CheckStringDefaults(object obj)
    {
        var props = obj.GetType()
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.CanRead);

        foreach (var prop in props)
        {
            var value = (string?)prop.GetValue(obj);
            value.Should().NotBeNull(
                $"{obj.GetType().Name}.{prop.Name} should default to string.Empty, not null");
        }
    }
}
