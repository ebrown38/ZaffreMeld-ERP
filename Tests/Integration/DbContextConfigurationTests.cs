using FluentAssertions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Models.Orders;

namespace ZaffreMeld.Tests.Integration;

/// <summary>
/// Tests that verify the DbContext's EF Core configuration is correct:
/// composite keys are honoured, DbSets resolve, and the seeded data is queryable.
/// </summary>
public class DbContextConfigurationTests : IDisposable
{
    private readonly ZaffreMeld.Web.Data.ZaffreMeldDbContext _db;

    public DbContextConfigurationTests()
    {
        _db = TestDbFactory.CreateSeeded();
    }

    public void Dispose() => _db.Dispose();

    // ── DbSet availability ─────────────────────────────────────────────────────

    [Fact]
    public void DbContext_AcctMstr_DbSetAccessible()
    {
        _db.AcctMstr.Should().NotBeNull();
        _db.AcctMstr.Count().Should().Be(5);
    }

    [Fact]
    public void DbContext_ItemMstr_DbSetAccessible()
    {
        _db.ItemMstr.Should().NotBeNull();
        _db.ItemMstr.Count().Should().Be(3);
    }

    [Fact]
    public void DbContext_CmMstr_DbSetAccessible()
    {
        _db.CmMstr.Count().Should().Be(3);
    }

    [Fact]
    public void DbContext_Counters_DbSetAccessible()
    {
        _db.Counters.Count().Should().Be(1);
    }

    [Fact]
    public void DbContext_GlTran_DbSetAccessible()
    {
        _db.GlTran.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_EdiMstr_DbSetAccessible()
    {
        _db.EdiMstr.Should().NotBeNull();
    }

    // ── Seeded data integrity ──────────────────────────────────────────────────

    [Fact]
    public void SeededItems_AllHaveCorrectSite()
    {
        _db.ItemMstr.Should().OnlyContain(i => i.ItSite == "DEFAULT");
    }

    [Fact]
    public void SeededAccounts_HaveExpectedTypes()
    {
        _db.AcctMstr.Where(a => a.Type == "A").Should().HaveCount(2); // 1000, 1100
        _db.AcctMstr.Where(a => a.Type == "L").Should().HaveCount(1); // 2000
        _db.AcctMstr.Where(a => a.Type == "R").Should().HaveCount(1); // 4000
        _db.AcctMstr.Where(a => a.Type == "X").Should().HaveCount(1); // 5000
    }

    [Fact]
    public void SeededCounter_HasCorrectConfiguration()
    {
        var counter = _db.Counters.Single(c => c.CounterName == "SO");
        counter.CounterPrefix.Should().Be("SO-");
        counter.CounterValue.Should().Be(1000);
        counter.CounterLength.Should().Be(6);
        counter.CounterSite.Should().Be("DEFAULT");
    }

    // ── Composite key queries ──────────────────────────────────────────────────

    [Fact]
    public async Task ItemCost_CompositeKeyFind_WorksCorrectly()
    {
        var cost = await _db.ItemCost.FindAsync("WIDGET-100", "DEFAULT", "STD");

        cost.Should().NotBeNull();
        cost!.ItcTotalcost.Should().Be(12.50m);
    }

    [Fact]
    public async Task SodDet_CompositeKey_CanInsertMultipleLinesPerOrder()
    {
        _db.SodDet.AddRange(
            new SodDet { SodNbr = "SO-TEST", SodLine = 10, SodItem = "WIDGET-100", SodStatus = "O" },
            new SodDet { SodNbr = "SO-TEST", SodLine = 20, SodItem = "GADGET-200", SodStatus = "O" },
            new SodDet { SodNbr = "SO-TEST", SodLine = 30, SodItem = "OBSOLETE",   SodStatus = "O" }
        );
        await _db.SaveChangesAsync();

        _db.SodDet.Count(l => l.SodNbr == "SO-TEST").Should().Be(3);
    }

    [Fact]
    public async Task ExcMstr_CompositeKey_WorksForCurrencyPairs()
    {
        _db.ExcMstr.Add(new ExcMstr
        {
            ExcBase    = "USD",
            ExcForeign = "EUR",
            ExcRate    = 0.92m,
            ExcEffDate = DateTime.Today
        });
        await _db.SaveChangesAsync();

        var rate = await _db.ExcMstr.FindAsync("USD", "EUR");
        rate.Should().NotBeNull();
        rate!.ExcRate.Should().Be(0.92m);
    }

    [Fact]
    public async Task FtpAttr_CompositeKey_StoresMultipleAttrsPerProfile()
    {
        _db.FtpMstr.Add(new FtpMstr { FtpId = "FTP-TEST", FtpDesc = "Test FTP", FtpIp = "10.0.0.1" });
        _db.FtpAttrs.AddRange(
            new ZaffreMeld.Web.Models.Administration.FtpAttr { FtpaId = "FTP-TEST", FtpaKey = "timeout", FtpaValue = "30" },
            new ZaffreMeld.Web.Models.Administration.FtpAttr { FtpaId = "FTP-TEST", FtpaKey = "mode",    FtpaValue = "passive" }
        );
        await _db.SaveChangesAsync();

        _db.FtpAttrs.Count(a => a.FtpaId == "FTP-TEST").Should().Be(2);
    }

    [Fact]
    public async Task EdiDocdet_CompositeKey_CanInsertRows()
    {
        _db.EdiDoc.Add(new EdiDoc { EddId = "DOC-001", EddDesc = "Test Doc", EddType = "850", EddPartner = "ACME", EddDir = "IN", EddActive = true });
        _db.EdiDocdet.AddRange(
            new EdiDocdet { EdidId = "DOC-001", EdidRole = "H", EdidRectype = "BEG", EdidSeq = 1, EdidDesc = "Beginning" },
            new EdiDocdet { EdidId = "DOC-001", EdidRole = "D", EdidRectype = "PO1", EdidSeq = 2, EdidDesc = "Line" }
        );
        await _db.SaveChangesAsync();

        _db.EdiDocdet.Count(d => d.EdidId == "DOC-001").Should().Be(2);
    }

    // ── LINQ query patterns ────────────────────────────────────────────────────

    [Fact]
    public void Can_Filter_ActiveItems()
    {
        var active = _db.ItemMstr.Where(i => i.ItStatus == "A").ToList();
        active.Should().HaveCount(2);
        active.Should().NotContain(i => i.ItItem == "OBSOLETE");
    }

    [Fact]
    public void Can_Filter_AssetAccounts()
    {
        var assets = _db.AcctMstr
            .Where(a => a.Type == "A")
            .OrderBy(a => a.Id)
            .ToList();

        assets.First().Id.Should().Be("1000");
        assets.Last().Id.Should().Be("1100");
    }

    [Fact]
    public void Can_Sum_ItemQoh()
    {
        var totalQoh = _db.ItemMstr.Sum(i => i.ItQoh);
        totalQoh.Should().Be(150m); // 100 + 50 + 0
    }
}
