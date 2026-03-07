using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

/// <summary>
/// Tests for OrdersController inline logic — browsing, filtering, pagination,
/// ship-to lookup, xref, terms, salespersons.
/// SO CRUD is covered by OrderServiceTests; these focus on controller plumbing.
/// </summary>
public class OrdersApiControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IOrderService> _svcMock;
    private readonly OrdersController _ctrl;

    public OrdersApiControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _svcMock = new Mock<IOrderService>();

        // Wire up service mock to use the in-memory db
        _svcMock.Setup(s => s.GetSalesOrder(It.IsAny<string>()))
            .ReturnsAsync((string n) => _db.SoMstr.Find(n));
        _svcMock.Setup(s => s.GetSalesOrderLines(It.IsAny<string>()))
            .ReturnsAsync((string n) => _db.SodDet.Where(l => l.SodNbr == n).ToList());
        _svcMock.Setup(s => s.CreateSalesOrder(It.IsAny<SoMstr>(), It.IsAny<List<SodDet>>()))
            .ReturnsAsync(ServiceResult.Ok("Created."));
        _svcMock.Setup(s => s.UpdateSalesOrder(It.IsAny<SoMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Updated."));
        _svcMock.Setup(s => s.CloseOrder(It.IsAny<string>()))
            .ReturnsAsync(ServiceResult.Ok("Closed."));
        _svcMock.Setup(s => s.GetCustomer(It.IsAny<string>()))
            .ReturnsAsync((string c) => _db.CmMstr.Find(c));
        _svcMock.Setup(s => s.SearchCustomers(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string q, int max) => _db.CmMstr
                .Where(c => c.CmCode.Contains(q) || c.CmName.Contains(q))
                .Take(max).ToList());
        _svcMock.Setup(s => s.AddCustomer(It.IsAny<CmMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Customer added."));
        _svcMock.Setup(s => s.UpdateCustomer(It.IsAny<CmMstr>()))
            .ReturnsAsync(ServiceResult.Ok("Customer updated."));

        // Seed orders
        _db.SoMstr.AddRange(
            new SoMstr { SoNbr = "SO-A001", SoCust = "ACME",   SoSite = "DEFAULT", SoStatus = "O", SoEntdate = "2026-03-01", SoTotalamt = 250m },
            new SoMstr { SoNbr = "SO-A002", SoCust = "ACME",   SoSite = "DEFAULT", SoStatus = "C", SoEntdate = "2026-02-01", SoTotalamt = 500m },
            new SoMstr { SoNbr = "SO-G001", SoCust = "GLOBEX", SoSite = "DEFAULT", SoStatus = "O", SoEntdate = "2026-03-02", SoTotalamt = 750m },
            new SoMstr { SoNbr = "SO-G002", SoCust = "GLOBEX", SoSite = "WEST",    SoStatus = "O", SoEntdate = "2026-03-03", SoTotalamt = 100m }
        );

        // Seed customer ship-tos
        _db.CmsDet.AddRange(
            new CmsDet { CmsCode = "ACME", CmsShipto = "WH-MAIN", CmsName = "Acme Main Warehouse", CmsCity = "Houston" },
            new CmsDet { CmsCode = "ACME", CmsShipto = "WH-WEST", CmsName = "Acme West Facility",  CmsCity = "Phoenix" }
        );

        // Seed customer xref
        _db.CupMstr.AddRange(
            new CupMstr { CupCust = "ACME", CupItem = "WIDGET-100", CupCitem = "ACME-WDG", CupActive = true },
            new CupMstr { CupCust = "ACME", CupItem = "GADGET-200", CupCitem = "ACME-GDG", CupActive = true }
        );

        // Seed terms
        _db.CustTerms.AddRange(
            new CustTerm { CutCode = "NET30", CutDesc = "Net 30 Days", CutDays = 30, CutActive = true },
            new CustTerm { CutCode = "NET60", CutDesc = "Net 60 Days", CutDays = 60, CutActive = true },
            new CustTerm { CutCode = "OLD",   CutDesc = "Obsolete",    CutDays = 0,  CutActive = false }
        );

        // Seed salespersons
        _db.SlspMstr.AddRange(
            new SlspMstr { SlspId = "SP-001", SlspName = "Alice Smith", SlspActive = true },
            new SlspMstr { SlspId = "SP-002", SlspName = "Bob Jones",   SlspActive = true },
            new SlspMstr { SlspId = "SP-OLD", SlspName = "Retired Rep", SlspActive = false }
        );

        _db.SaveChanges();

        _ctrl = new OrdersController(_svcMock.Object, _db, NullLogger<OrdersController>.Instance);
        SetUser(_ctrl, "ordersuser");
    }

    public void Dispose() => _db.Dispose();

    // ── GetSalesOrder ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesOrder_ExistingOrder_Returns200WithHeaderAndLines()
    {
        var result = await _ctrl.GetSalesOrder("SO-A001") as OkObjectResult;
        result.Should().NotBeNull();
        var body = result!.Value;
        Anon.Prop<object>(body, "header").Should().NotBeNull();
    }

    [Fact]
    public async Task GetSalesOrder_NonExistent_Returns404()
    {
        _svcMock.Setup(s => s.GetSalesOrder("GHOST")).ReturnsAsync((SoMstr?)null);
        (await _ctrl.GetSalesOrder("GHOST")).Should().BeOfType<NotFoundResult>();
    }

    // ── GetSalesOrders (browse) ────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesOrders_NoFilter_ReturnsAll()
    {
        var result = await _ctrl.GetSalesOrders() as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(4);
    }

    [Fact]
    public async Task GetSalesOrders_FiltersByCustomer()
    {
        var result = await _ctrl.GetSalesOrders(cust: "ACME") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(2);
    }

    [Fact]
    public async Task GetSalesOrders_FiltersByStatus()
    {
        var result = await _ctrl.GetSalesOrders(status: "O") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(3);
    }

    [Fact]
    public async Task GetSalesOrders_FiltersBySite()
    {
        var result = await _ctrl.GetSalesOrders(site: "WEST") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(1);
    }

    [Fact]
    public async Task GetSalesOrders_CombinesFilters()
    {
        var result = await _ctrl.GetSalesOrders(cust: "GLOBEX", status: "O") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(2);
    }

    [Fact]
    public async Task GetSalesOrders_OrdersByEntdateDescending()
    {
        var result = await _ctrl.GetSalesOrders() as OkObjectResult;
        var body   = result!.Value;
        var orders = Anon.Prop<List<SoMstr>>(body, "orders");
        string.Compare(orders[0].SoEntdate, orders[1].SoEntdate).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSalesOrders_Pagination_RespectsPageSize()
    {
        var result = await _ctrl.GetSalesOrders(pageSize: 2) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(4);
        Anon.Prop<List<SoMstr>>(body, "orders").Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSalesOrders_Page2_ReturnsCorrectSlice()
    {
        var result = await _ctrl.GetSalesOrders(page: 2, pageSize: 2) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<List<SoMstr>>(body, "orders").Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSalesOrders_PageBeyondData_ReturnsEmpty()
    {
        var result = await _ctrl.GetSalesOrders(page: 99, pageSize: 10) as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(4);
        Anon.Prop<List<SoMstr>>(body, "orders").Should().BeEmpty();
    }

    // ── CreateSalesOrder ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSalesOrder_SetsUserFromClaimsPrincipal()
    {
        SoMstr? capturedSo = null;
        _svcMock.Setup(s => s.CreateSalesOrder(It.IsAny<SoMstr>(), It.IsAny<List<SodDet>>()))
            .Callback<SoMstr, List<SodDet>>((so, _) => capturedSo = so)
            .ReturnsAsync(ServiceResult.Ok("Created."));

        var req = new CreateSalesOrderRequest(
            new SoMstr { SoNbr = "SO-NEW", SoCust = "ACME", SoSite = "DEFAULT" },
            new List<SodDet>());

        await _ctrl.CreateSalesOrder(req);

        capturedSo!.SoUser.Should().Be("ordersuser");
    }

    [Fact]
    public async Task CreateSalesOrder_Success_Returns200()
    {
        var req = new CreateSalesOrderRequest(
            new SoMstr { SoNbr = "SO-NEW2", SoCust = "ACME", SoSite = "DEFAULT" },
            new List<SodDet>());
        (await _ctrl.CreateSalesOrder(req)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateSalesOrder_ServiceFails_Returns400()
    {
        _svcMock.Setup(s => s.CreateSalesOrder(It.IsAny<SoMstr>(), It.IsAny<List<SodDet>>()))
            .ReturnsAsync(ServiceResult.Error("Customer not found."));

        var req = new CreateSalesOrderRequest(
            new SoMstr { SoNbr = "SO-BAD", SoCust = "NOBODY" },
            new List<SodDet>());
        (await _ctrl.CreateSalesOrder(req)).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateSalesOrder ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSalesOrder_NumberMismatch_Returns400()
    {
        var so = new SoMstr { SoNbr = "SO-WRONG" };
        (await _ctrl.UpdateSalesOrder("SO-A001", so)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateSalesOrder_Success_Returns200()
    {
        var so = new SoMstr { SoNbr = "SO-A001", SoCust = "ACME", SoNote = "Updated" };
        (await _ctrl.UpdateSalesOrder("SO-A001", so)).Should().BeOfType<OkObjectResult>();
    }

    // ── CloseSalesOrder ────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseSalesOrder_Success_Returns200()
    {
        (await _ctrl.CloseSalesOrder("SO-A001")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CloseSalesOrder_ServiceFails_Returns400()
    {
        _svcMock.Setup(s => s.CloseOrder("GHOST")).ReturnsAsync(ServiceResult.Error("Not found."));
        (await _ctrl.CloseSalesOrder("GHOST")).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Customer endpoints ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomer_ExistingCode_Returns200()
    {
        (await _ctrl.GetCustomer("ACME")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCustomer_NonExistent_Returns404()
    {
        _svcMock.Setup(s => s.GetCustomer("GHOST")).ReturnsAsync((CmMstr?)null);
        (await _ctrl.GetCustomer("GHOST")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SearchCustomers_ReturnsMatches()
    {
        var result = await _ctrl.SearchCustomers("ACME") as OkObjectResult;
        ((List<CmMstr>)result.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task AddCustomer_Success_Returns200()
    {
        var cust = new CmMstr { CmCode = "NEWCO", CmName = "New Co", CmStatus = "A" };
        (await _ctrl.AddCustomer(cust)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddCustomer_ServiceFails_Returns400()
    {
        _svcMock.Setup(s => s.AddCustomer(It.IsAny<CmMstr>()))
            .ReturnsAsync(ServiceResult.Error("Already exists."));
        var cust = new CmMstr { CmCode = "ACME", CmName = "Dup" };
        (await _ctrl.AddCustomer(cust)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCustomer_CodeMismatch_Returns400()
    {
        var cust = new CmMstr { CmCode = "WRONG" };
        (await _ctrl.UpdateCustomer("ACME", cust)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCustomer_Success_Returns200()
    {
        var cust = new CmMstr { CmCode = "ACME", CmName = "Acme (Updated)" };
        (await _ctrl.UpdateCustomer("ACME", cust)).Should().BeOfType<OkObjectResult>();
    }

    // ── Ship-tos ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetCustomerShipTos_ReturnsAllShipTos()
    {
        var result = _ctrl.GetCustomerShipTos("ACME") as OkObjectResult;
        ((List<CmsDet>)result.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetCustomerShipTos_NoShipTos_ReturnsEmpty()
    {
        var result = _ctrl.GetCustomerShipTos("GLOBEX") as OkObjectResult;
        ((List<CmsDet>)result.Value!).Should().BeEmpty();
    }

    // ── Customer Xref ──────────────────────────────────────────────────────────

    [Fact]
    public void GetCustomerXref_NoCustFilter_ReturnsAllForCust()
    {
        var result = _ctrl.GetCustomerXref("ACME") as OkObjectResult;
        ((List<CupMstr>)result.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetCustomerXref_FiltersByItem_ReturnsSingleXref()
    {
        var result = _ctrl.GetCustomerXref("ACME", item: "WIDGET-100") as OkObjectResult;
        var xrefs  = ((List<CupMstr>)result.Value!);
        xrefs.Should().HaveCount(1);
        xrefs.Single().CupCitem.Should().Be("ACME-WDG");
    }

    [Fact]
    public void GetCustomerXref_NoMatch_ReturnsEmpty()
    {
        var result = _ctrl.GetCustomerXref("GLOBEX") as OkObjectResult;
        ((List<CupMstr>)result.Value!).Should().BeEmpty();
    }

    // ── Terms ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GetTerms_ReturnsActiveTerms_OrderedByCode()
    {
        var result = _ctrl.GetTerms() as OkObjectResult;
        var terms  = ((List<CustTerm>)result.Value!);
        terms.Should().HaveCount(2);
        terms.Should().NotContain(t => t.CutCode == "OLD");
        terms[0].CutCode.Should().Be("NET30");
        terms[1].CutCode.Should().Be("NET60");
    }

    // ── Salespersons ───────────────────────────────────────────────────────────

    [Fact]
    public void GetSalespersons_ReturnsActiveOnly_OrderedByName()
    {
        var result = _ctrl.GetSalespersons() as OkObjectResult;
        var slsps  = ((List<SlspMstr>)result.Value!);
        slsps.Should().HaveCount(2);
        slsps.Should().NotContain(s => s.SlspId == "SP-OLD");
        slsps[0].SlspName.Should().Be("Alice Smith");
        slsps[1].SlspName.Should().Be("Bob Jones");
    }

    // ── Pricing ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetPricing_NoPrices_ReturnsEmpty()
    {
        var result = _ctrl.GetPricing() as OkObjectResult;
        ((List<CprMstr>)result.Value!).Should().BeEmpty();
    }

    [Fact]
    public void GetPricing_FiltersByCustomer()
    {
        _db.CprMstr.AddRange(
            new CprMstr { CprCust = "ACME",   CprItem = "WIDGET-100", CprPrice = 15m, CprMinqty = 1m, CprUom = "EA", CprCurrency = "USD", CprActive = true, CprEfffrom = "", CprEffthru = "" },
            new CprMstr { CprCust = "GLOBEX", CprItem = "WIDGET-100", CprPrice = 14m, CprMinqty = 1m, CprUom = "EA", CprCurrency = "USD", CprActive = true, CprEfffrom = "", CprEffthru = "" }
        );
        _db.SaveChanges();

        var result = _ctrl.GetPricing(cust: "ACME") as OkObjectResult;
        ((List<CprMstr>)result.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetPricing_ExcludesInactive()
    {
        _db.CprMstr.Add(new CprMstr { CprCust = "ACME", CprItem = "WIDGET-100", CprPrice = 9m, CprMinqty = 1m, CprUom = "EA", CprCurrency = "USD", CprActive = false, CprEfffrom = "", CprEffthru = "" });
        _db.SaveChanges();

        var result = _ctrl.GetPricing(cust: "ACME") as OkObjectResult;
        ((List<CprMstr>)result.Value!).Should().BeEmpty();
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