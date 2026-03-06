using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Tests.Unit;

public class FinanceServiceTests : IDisposable
{
    private readonly ZaffreMeld.Web.Data.ZaffreMeldDbContext _db;
    private readonly FinanceService _svc;

    public FinanceServiceTests()
    {
        _db  = TestDbFactory.CreateSeeded();
        _svc = new FinanceService(_db, NullLogger<FinanceService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── GetAccount ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccount_ExistingId_ReturnsAccount()
    {
        var acct = await _svc.GetAccount("1000");

        acct.Should().NotBeNull();
        acct!.Desc.Should().Be("Cash");
        acct.Type.Should().Be("A");
    }

    [Fact]
    public async Task GetAccount_NonExistentId_ReturnsNull()
    {
        var acct = await _svc.GetAccount("9999");
        acct.Should().BeNull();
    }

    // ── GetAccountsInRange ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountsInRange_IncludesEndpoints()
    {
        var accounts = await _svc.GetAccountsInRange("1000", "2000");

        accounts.Should().HaveCount(3);
        accounts.Select(a => a.Id).Should().Contain(new[] { "1000", "1100", "2000" });
    }

    [Fact]
    public async Task GetAccountsInRange_ExcludesOutsideRange()
    {
        var accounts = await _svc.GetAccountsInRange("4000", "5000");

        accounts.Should().HaveCount(2);
        accounts.Select(a => a.Id).Should().NotContain("1000");
    }

    [Fact]
    public async Task GetAccountsInRange_EmptyRange_ReturnsEmpty()
    {
        var accounts = await _svc.GetAccountsInRange("8000", "9000");
        accounts.Should().BeEmpty();
    }

    // ── AddAccount ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAccount_NewAccount_Succeeds()
    {
        var acct = new AcctMstr { Id = "6000", Desc = "Other Expense", Type = "X", Site = "DEFAULT" };

        var result = await _svc.AddAccount(acct);

        result.Success.Should().BeTrue();
        (await _svc.GetAccount("6000")).Should().NotBeNull();
    }

    [Fact]
    public async Task AddAccount_DuplicateId_ReturnsError()
    {
        var acct = new AcctMstr { Id = "1000", Desc = "Duplicate Cash", Type = "A", Site = "DEFAULT" };

        var result = await _svc.AddAccount(acct);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    // ── UpdateAccount ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAccount_ChangesDescription()
    {
        var acct = await _svc.GetAccount("1000");
        acct!.Desc = "Cash & Equivalents";

        var result = await _svc.UpdateAccount(acct);

        result.Success.Should().BeTrue();
        (await _svc.GetAccount("1000"))!.Desc.Should().Be("Cash & Equivalents");
    }

    // ── DeleteAccount ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_ExistingAccount_RemovesIt()
    {
        var result = await _svc.DeleteAccount("5000");

        result.Success.Should().BeTrue();
        (await _svc.GetAccount("5000")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAccount_NonExistent_ReturnsError()
    {
        var result = await _svc.DeleteAccount("9999");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    // ── PostGlPair ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostGlPair_CreatesDebitAndCreditTransactions()
    {
        var pair = new GlPair
        {
            GlvAcctDr  = "5000", GlvCcDr  = "CC1",
            GlvAcctCr  = "1000", GlvCcCr  = "CC1",
            GlvAmt     = 500m,   GlvBaseAmt = 500m,
            GlvCurr    = "USD",  GlvRef   = "TEST-001",
            GlvEffdate = "2026-03-01",
            GlvType    = "AP",   GlvDesc  = "Test GL pair",
            GlvDoc     = "DOC-1", GlvSite = "DEFAULT"
        };

        var result = await _svc.PostGlPair(pair);

        result.Success.Should().BeTrue();

        var trans = _db.GlTran.Where(t => t.GltRef == "TEST-001").ToList();
        trans.Should().HaveCount(2);

        var debit  = trans.Single(t => t.GltAmt > 0);
        var credit = trans.Single(t => t.GltAmt < 0);

        debit.GltAcct.Should().Be("5000");
        debit.GltAmt.Should().Be(500m);
        credit.GltAcct.Should().Be("1000");
        credit.GltAmt.Should().Be(-500m);
    }

    [Fact]
    public async Task PostGlPair_DebitsAndCreditsBalance_ToZero()
    {
        var pair = new GlPair
        {
            GlvAcctDr = "5000", GlvCcDr = "CC1",
            GlvAcctCr = "1000", GlvCcCr = "CC1",
            GlvAmt = 1234.56m, GlvBaseAmt = 1234.56m,
            GlvCurr = "USD", GlvRef = "BALANCE-TEST",
            GlvEffdate = "2026-03-01", GlvType = "AR",
            GlvDoc = "DOC-2", GlvSite = "DEFAULT"
        };

        await _svc.PostGlPair(pair);

        var net = _db.GlTran
            .Where(t => t.GltRef == "BALANCE-TEST")
            .Sum(t => t.GltAmt);

        net.Should().Be(0m);
    }

    [Fact]
    public async Task PostGlPair_UsesTodayWhenEffdateEmpty()
    {
        var pair = new GlPair
        {
            GlvAcctDr = "5000", GlvCcDr = "CC1",
            GlvAcctCr = "1000", GlvCcCr = "CC1",
            GlvAmt = 100m, GlvBaseAmt = 100m,
            GlvCurr = "USD", GlvRef = "NO-DATE",
            GlvEffdate = string.Empty, GlvType = "AR",
            GlvDoc = "DOC-3", GlvSite = "DEFAULT"
        };

        await _svc.PostGlPair(pair);

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        _db.GlTran
            .Where(t => t.GltRef == "NO-DATE")
            .All(t => t.GltEffdate == today)
            .Should().BeTrue();
    }

    // ── PostGlTransactions (batch) ─────────────────────────────────────────────

    [Fact]
    public async Task PostGlTransactions_BatchInsertAll()
    {
        var batch = Enumerable.Range(1, 5).Select(i => new GlTran
        {
            GltAcct = "4000", GltCc = "CC1",
            GltAmt  = i * 100m, GltRef = $"BATCH-{i}",
            GltEffdate = "2026-03-01", GltType = "AR",
            GltDoc = $"DOC-B{i}", GltSite = "DEFAULT"
        }).ToList();

        var result = await _svc.PostGlTransactions(batch);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("5");
        _db.GlTran.Count(t => t.GltRef.StartsWith("BATCH-")).Should().Be(5);
    }

    // ── GetGlTransactions ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetGlTransactions_FiltersByAccount()
    {
        _db.GlTran.AddRange(
            new GlTran { GltAcct = "1000", GltRef = "R1", GltAmt = 100m, GltEffdate = "2026-01-01", GltSite = "DEFAULT" },
            new GlTran { GltAcct = "4000", GltRef = "R2", GltAmt = 200m, GltEffdate = "2026-01-01", GltSite = "DEFAULT" }
        );
        await _db.SaveChangesAsync();

        var trans = await _svc.GetGlTransactions("1000");

        trans.Should().HaveCount(1);
        trans.Single().GltRef.Should().Be("R1");
    }

    [Fact]
    public async Task GetGlTransactions_FiltersByDateRange()
    {
        _db.GlTran.AddRange(
            new GlTran { GltAcct = "1000", GltRef = "OLD", GltAmt = 10m, GltEffdate = "2025-01-01", GltSite = "DEFAULT" },
            new GlTran { GltAcct = "1000", GltRef = "NEW", GltAmt = 20m, GltEffdate = "2026-03-01", GltSite = "DEFAULT" }
        );
        await _db.SaveChangesAsync();

        var trans = await _svc.GetGlTransactions("1000", fromDate: "2026-01-01");

        trans.Should().HaveCount(1);
        trans.Single().GltRef.Should().Be("NEW");
    }

    // ── GetAccountBalance ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountBalance_SumsAmountsForPeriod()
    {
        _db.GlTran.AddRange(
            new GlTran { GltAcct = "4000", GltCc = "CC1", GltYear = "2026", GltPeriod = "03", GltAmt = 1000m, GltEffdate = "2026-03-01", GltSite = "DEFAULT" },
            new GlTran { GltAcct = "4000", GltCc = "CC1", GltYear = "2026", GltPeriod = "03", GltAmt = 500m,  GltEffdate = "2026-03-15", GltSite = "DEFAULT" },
            new GlTran { GltAcct = "4000", GltCc = "CC1", GltYear = "2026", GltPeriod = "02", GltAmt = 999m,  GltEffdate = "2026-02-15", GltSite = "DEFAULT" }
        );
        await _db.SaveChangesAsync();

        var balance = await _svc.GetAccountBalance("4000", "CC1", "2026", "03");

        balance.Should().Be(1500m);
    }

    [Fact]
    public async Task GetAccountBalance_NoTransactions_ReturnsZero()
    {
        var balance = await _svc.GetAccountBalance("4000", "CC1", "2099", "12");
        balance.Should().Be(0m);
    }
}
