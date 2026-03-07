using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

public class FinanceApiControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IFinanceService> _svcMock;
    private readonly FinanceController _ctrl;

    public FinanceApiControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _svcMock = new Mock<IFinanceService>();

        // Default happy-path setups
        _svcMock.Setup(s => s.GetAccount(It.IsAny<string>()))
            .ReturnsAsync((string id) => _db.AcctMstr.Find(id));
        var allAccounts = _db.AcctMstr.OrderBy(a => a.Id).ToList();
        _svcMock.Setup(s => s.GetAccountsInRange(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string f, string t) => Task.FromResult(
                allAccounts
                    .Where(a => string.Compare(a.Id, f, StringComparison.Ordinal) >= 0
                             && string.Compare(a.Id, t, StringComparison.Ordinal) <= 0)
                    .ToList()));
        _svcMock.Setup(s => s.AddAccount(It.IsAny<AcctMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Account added successfully."));
        _svcMock.Setup(s => s.UpdateAccount(It.IsAny<AcctMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Account updated successfully."));
        _svcMock.Setup(s => s.DeleteAccount(It.IsAny<string>()))
            .ReturnsAsync(ServiceResult.Ok("Account deleted."));
        _svcMock.Setup(s => s.PostGlPair(It.IsAny<GlPair>()))
            .ReturnsAsync(ServiceResult.Ok());
        _svcMock.Setup(s => s.PostGlTransaction(It.IsAny<GlTran>()))
            .ReturnsAsync(ServiceResult.Ok());
        _svcMock.Setup(s => s.PostGlTransactions(It.IsAny<List<GlTran>>()))
            .ReturnsAsync(ServiceResult.Ok("5 transactions posted."));
        _svcMock.Setup(s => s.GetGlTransactions(It.IsAny<string>(), null, null))
            .ReturnsAsync(new List<GlTran>());
        _svcMock.Setup(s => s.GetAccountBalance(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(1500m);

        _ctrl = new FinanceController(_svcMock.Object, _db, NullLogger<FinanceController>.Instance);
        SetUser(_ctrl, "finuser");
    }

    public void Dispose() => _db.Dispose();

    // ── Chart of Accounts ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccount_ExistingId_Returns200()
    {
        var result = await _ctrl.GetAccount("1000");
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAccount_NonExistent_Returns404()
    {
        _svcMock.Setup(s => s.GetAccount("9999")).ReturnsAsync((AcctMstr?)null);
        (await _ctrl.GetAccount("9999")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAccounts_DefaultRange_ReturnsAll()
    {
        var result = await _ctrl.GetAccounts() as OkObjectResult;
        ((List<AcctMstr>)result.Value!).Should().HaveCount(5);
    }

    [Fact]
    public async Task AddAccount_Success_Returns200()
    {
        var acct = new AcctMstr { Id = "6000", Desc = "Other", Type = "X", Site = "DEFAULT" };
        (await _ctrl.AddAccount(acct)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddAccount_Failure_Returns400()
    {
        _svcMock.Setup(s => s.AddAccount(It.IsAny<AcctMstr>()))
            .ReturnsAsync(ServiceResult.Error("Already exists."));
        (await _ctrl.AddAccount(new AcctMstr { Id = "1000" })).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAccount_IdMismatch_Returns400()
    {
        var acct = new AcctMstr { Id = "9000" };
        (await _ctrl.UpdateAccount("1000", acct)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAccount_Success_Returns200()
    {
        var acct = new AcctMstr { Id = "1000", Desc = "Updated Cash", Type = "A", Site = "DEFAULT" };
        (await _ctrl.UpdateAccount("1000", acct)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteAccount_Success_Returns200()
    {
        (await _ctrl.DeleteAccount("1000")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteAccount_Failure_Returns400()
    {
        _svcMock.Setup(s => s.DeleteAccount("GHOST")).ReturnsAsync(ServiceResult.Error("Not found."));
        (await _ctrl.DeleteAccount("GHOST")).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GL Transactions ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGlTran_ReturnsTransactions()
    {
        _svcMock.Setup(s => s.GetGlTransactions("4000", null, null))
            .ReturnsAsync(new List<GlTran> { new GlTran { GltAcct = "4000", GltAmt = 1000m } });

        var result = await _ctrl.GetGlTran("4000") as OkObjectResult;
        ((List<GlTran>)result.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task PostGlTran_Success_Returns200()
    {
        var tran = new GlTran { GltAcct = "4000", GltAmt = 500m, GltEffdate = "2026-03-01", GltSite = "DEFAULT" };
        (await _ctrl.PostGlTran(tran)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PostGlTranBatch_Success_Returns200()
    {
        var batch = new List<GlTran> { new GlTran { GltAcct = "4000", GltAmt = 100m } };
        (await _ctrl.PostGlTranBatch(batch)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PostGlPair_Success_Returns200()
    {
        var pair = new GlPair { GlvAcctDr = "5000", GlvAcctCr = "1000", GlvAmt = 500m };
        (await _ctrl.PostGlPair(pair)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteGlTran_UnpostedTran_Returns200()
    {
        var tran = new GlTran
        {
            GltAcct = "4000", GltCc = "CC1", GltAmt = 100m,
            GltEffdate = "2026-03-01", GltSite = "DEFAULT", GltPosted = false
        };
        _db.GlTran.Add(tran);
        await _db.SaveChangesAsync();

        var result = await _ctrl.DeleteGlTran(tran.GltId);
        result.Should().BeOfType<OkObjectResult>();
        _db.GlTran.Find(tran.GltId).Should().BeNull();
    }

    [Fact]
    public async Task DeleteGlTran_PostedTran_Returns400()
    {
        var tran = new GlTran
        {
            GltAcct = "4000", GltCc = "CC1", GltAmt = 100m,
            GltEffdate = "2026-03-01", GltSite = "DEFAULT", GltPosted = true
        };
        _db.GlTran.Add(tran);
        await _db.SaveChangesAsync();

        (await _ctrl.DeleteGlTran(tran.GltId)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteGlTran_NonExistent_Returns404()
    {
        (await _ctrl.DeleteGlTran(99999)).Should().BeOfType<NotFoundResult>();
    }

    // ── Account Balance ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountBalance_ReturnsBalance()
    {
        var result = await _ctrl.GetAccountBalance("4000", "CC1", "2026", "03") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<decimal>(body, "balance").Should().Be(1500m);
    }

    [Fact]
    public async Task GetAccountBalance_ResponseIncludesAllParams()
    {
        var result = await _ctrl.GetAccountBalance("4000", "CC1", "2026", "03") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<string>(body, "account").Should().Be("4000");
        Anon.Prop<string>(body, "cc").Should().Be("CC1");
        Anon.Prop<string>(body, "year").Should().Be("2026");
        Anon.Prop<string>(body, "period").Should().Be("03");
    }

    // ── Banks ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBank_ExistingBank_Returns200()
    {
        _db.BankMstr.Add(new BankMstr { Id = "BANK-001", Desc = "Main Bank", Account = "123456", Routing = "021000021", Currency = "USD", CbActive = true, Site = "DEFAULT", GlAcct = "1000", GlCc = "CC1" });
        await _db.SaveChangesAsync();

        (await _ctrl.GetBank("BANK-001")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBank_NonExistent_Returns404()
    {
        (await _ctrl.GetBank("GHOST-BANK")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetBanks_ReturnsActiveBanks()
    {
        _db.BankMstr.AddRange(
            new BankMstr { Id = "BANK-A", Desc = "Active",   CbActive = true,  Site = "DEFAULT", Account = "001", Routing = "001", Currency = "USD", GlAcct = "1000", GlCc = "CC1" },
            new BankMstr { Id = "BANK-B", Desc = "Inactive", CbActive = false, Site = "DEFAULT", Account = "002", Routing = "002", Currency = "USD", GlAcct = "1000", GlCc = "CC1" }
        );
        _db.SaveChanges();

        var result = _ctrl.GetBanks() as OkObjectResult;
        ((List<BankMstr>)result.Value!).Should().OnlyContain(b => b.CbActive);
    }

    // ── Currencies ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetCurrencies_ReturnsAll()
    {
        _db.CurrMstr.AddRange(
            new CurrMstr { Id = "USD", Desc = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsBase = true },
            new CurrMstr { Id = "EUR", Desc = "Euro",      Symbol = "€", DecimalPlaces = 2, IsBase = false }
        );
        _db.SaveChanges();

        var result = _ctrl.GetCurrencies() as OkObjectResult;
        ((List<CurrMstr>)result.Value!).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCurrency_ExistingId_Returns200()
    {
        _db.CurrMstr.Add(new CurrMstr { Id = "GBP", Desc = "British Pound", Symbol = "£", DecimalPlaces = 2 });
        await _db.SaveChangesAsync();

        (await _ctrl.GetCurrency("GBP")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCurrency_NonExistent_Returns404()
    {
        (await _ctrl.GetCurrency("XYZ")).Should().BeOfType<NotFoundResult>();
    }

    // ── Exchange Rates ─────────────────────────────────────────────────────────

    [Fact]
    public void GetExchangeRates_ReturnsRatesForBase()
    {
        _db.ExcMstr.AddRange(
            new ExcMstr { ExcBase = "USD", ExcForeign = "EUR", ExcRate = 0.92m },
            new ExcMstr { ExcBase = "USD", ExcForeign = "GBP", ExcRate = 0.79m },
            new ExcMstr { ExcBase = "EUR", ExcForeign = "GBP", ExcRate = 0.86m }
        );
        _db.SaveChanges();

        var result = _ctrl.GetExchangeRates("USD") as OkObjectResult;
        var rates  = ((List<ExcMstr>)result.Value!);
        rates.Should().HaveCount(2);
        rates.Should().OnlyContain(r => r.ExcBase == "USD");
    }

    // ── AR ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAr_ExistingInvoice_Returns200()
    {
        _db.ArMstr.Add(new ArMstr { ArId = "INV-001", ArCust = "ACME", ArAmt = 500m, ArEntdate = "2026-03-01", ArSite = "DEFAULT", ArStatus = "O", ArDuedate = "2026-04-01" });
        await _db.SaveChangesAsync();

        (await _ctrl.GetAr("INV-001")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAr_NonExistent_Returns404()
    {
        (await _ctrl.GetAr("GHOST-INV")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetArLines_ReturnsLinesForInvoice()
    {
        _db.ArdMstr.AddRange(
            new ArdMstr { ArdId = "INV-002-1", ArdNbr = "INV-002", ArdLine = 1, ArdItem = "WIDGET-100", ArdAmt = 250m },
            new ArdMstr { ArdId = "INV-002-2", ArdNbr = "INV-002", ArdLine = 2, ArdItem = "GADGET-200", ArdAmt = 250m }
        );
        _db.SaveChanges();

        var result = _ctrl.GetArLines("INV-002") as OkObjectResult;
        ((List<ArdMstr>)result.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetArAging_FiltersBySiteAndOpenStatus()
    {
        _db.ArMstr.AddRange(
            new ArMstr { ArId = "INV-A", ArCust = "ACME", ArAmt = 100m, ArEntdate = "2026-03-01", ArSite = "DEFAULT", ArStatus = "O", ArDuedate = "2026-04-01" },
            new ArMstr { ArId = "INV-B", ArCust = "ACME", ArAmt = 200m, ArEntdate = "2026-02-01", ArSite = "DEFAULT", ArStatus = "C", ArDuedate = "2026-03-01" },
            new ArMstr { ArId = "INV-C", ArCust = "GLOBEX", ArAmt = 300m, ArEntdate = "2026-03-01", ArSite = "WEST", ArStatus = "O", ArDuedate = "2026-04-01" }
        );
        _db.SaveChanges();

        var result = _ctrl.GetArAging("DEFAULT") as OkObjectResult;
        var ar     = ((List<ArMstr>)result.Value!);
        ar.Should().HaveCount(1);
        ar.Single().ArId.Should().Be("INV-A");
    }

    [Fact]
    public void GetArAging_FiltersByCustomer()
    {
        _db.ArMstr.AddRange(
            new ArMstr { ArId = "INV-D", ArCust = "ACME",   ArAmt = 100m, ArSite = "DEFAULT", ArStatus = "O", ArDuedate = "2026-04-01", ArEntdate = "2026-03-01" },
            new ArMstr { ArId = "INV-E", ArCust = "GLOBEX", ArAmt = 200m, ArSite = "DEFAULT", ArStatus = "O", ArDuedate = "2026-04-15", ArEntdate = "2026-03-01" }
        );
        _db.SaveChanges();

        var result = _ctrl.GetArAging("DEFAULT", cust: "ACME") as OkObjectResult;
        ((List<ArMstr>)result.Value!).Should().OnlyContain(a => a.ArCust == "ACME");
    }

    // ── AP ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAp_ExistingVoucher_Returns200()
    {
        _db.ApMstr.Add(new ApMstr { ApId = "VCH-001", ApVend = "ACME-SUPPLY", ApSite = "DEFAULT", ApStatus = "O", ApDuedate = "2026-04-01" });
        await _db.SaveChangesAsync();

        (await _ctrl.GetAp("VCH-001")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAp_NonExistent_Returns404()
    {
        (await _ctrl.GetAp("GHOST-VCH")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetApAging_FiltersBySiteAndOpenStatus()
    {
        _db.ApMstr.AddRange(
            new ApMstr { ApId = "VCH-A", ApVend = "VEND-1", ApSite = "DEFAULT", ApStatus = "O", ApDuedate = "2026-04-01" },
            new ApMstr { ApId = "VCH-B", ApVend = "VEND-1", ApSite = "DEFAULT", ApStatus = "C", ApDuedate = "2026-03-01" }
        );
        _db.SaveChanges();

        var result = _ctrl.GetApAging("DEFAULT") as OkObjectResult;
        ((List<ApMstr>)result.Value!).Should().HaveCount(1);
    }

    // ── Departments ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDepartment_ExistingId_Returns200()
    {
        _db.DeptMstr.Add(new DeptMstr { DeptId = "PROD", DeptDesc = "Production", DeptSite = "DEFAULT", DeptActive = true });
        await _db.SaveChangesAsync();

        (await _ctrl.GetDepartment("PROD")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDepartment_NonExistent_Returns404()
    {
        (await _ctrl.GetDepartment("GHOST-DEPT")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetDepartments_ReturnsActiveDepartments()
    {
        _db.DeptMstr.AddRange(
            new DeptMstr { DeptId = "PROD",   DeptDesc = "Production", DeptSite = "DEFAULT", DeptActive = true },
            new DeptMstr { DeptId = "OLD",    DeptDesc = "Old Dept",   DeptSite = "DEFAULT", DeptActive = false }
        );
        _db.SaveChanges();

        var result = _ctrl.GetDepartments() as OkObjectResult;
        ((List<DeptMstr>)result.Value!).Should().OnlyContain(d => d.DeptActive);
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