using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Tests.Unit;

public class OrderServiceTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly OrderService _svc;

    public OrderServiceTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetNextDocumentNumber("SO")).ReturnsAsync("SO-001001");
        _appMock.Setup(a => a.GetSite()).Returns("DEFAULT");

        _svc = new OrderService(_db, _appMock.Object, NullLogger<OrderService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── CreateSalesOrder ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSalesOrder_WithLines_Succeeds()
    {
        var (so, lines) = BuildOrder();

        var result = await _svc.CreateSalesOrder(so, lines);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("created");
    }

    [Fact]
    public async Task CreateSalesOrder_AssignsNumberWhenEmpty()
    {
        var (so, lines) = BuildOrder();
        so.SoNbr = string.Empty;

        await _svc.CreateSalesOrder(so, lines);

        so.SoNbr.Should().Be("SO-001001");
        _appMock.Verify(a => a.GetNextDocumentNumber("SO"), Times.Once);
    }

    [Fact]
    public async Task CreateSalesOrder_PreservesExplicitNumber()
    {
        var (so, lines) = BuildOrder("SO-CUSTOM");

        await _svc.CreateSalesOrder(so, lines);

        so.SoNbr.Should().Be("SO-CUSTOM");
        _appMock.Verify(a => a.GetNextDocumentNumber(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateSalesOrder_SetsStatusToOpen()
    {
        var (so, lines) = BuildOrder();
        so.SoStatus = string.Empty;

        await _svc.CreateSalesOrder(so, lines);

        var saved = await _svc.GetSalesOrder(so.SoNbr);
        saved!.SoStatus.Should().Be("O");
    }

    [Fact]
    public async Task CreateSalesOrder_SetsEntryDateToToday()
    {
        var (so, lines) = BuildOrder();

        await _svc.CreateSalesOrder(so, lines);

        so.SoEntdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task CreateSalesOrder_AutonumbersLines_StartingAt10()
    {
        var (so, lines) = BuildOrder();
        foreach (var l in lines) l.SodLine = 0; // clear line numbers

        await _svc.CreateSalesOrder(so, lines);

        var savedLines = await _svc.GetSalesOrderLines(so.SoNbr);
        savedLines.Should().HaveCount(2);
        savedLines[0].SodLine.Should().Be(10);
        savedLines[1].SodLine.Should().Be(20);
    }

    [Fact]
    public async Task CreateSalesOrder_CalculatesTotalAmount()
    {
        // 10 × $25.00 + 5 × $49.99 = $499.95
        var (so, lines) = BuildOrder();

        await _svc.CreateSalesOrder(so, lines);

        var saved = await _svc.GetSalesOrder(so.SoNbr);
        saved!.SoTotalamt.Should().Be(499.95m);
    }

    [Fact]
    public async Task CreateSalesOrder_AppliesLineDiscount_ToTotal()
    {
        var (so, lines) = BuildOrder();
        lines[0].SodDisc = 10m; // 10% discount on first line

        await _svc.CreateSalesOrder(so, lines);

        // Line1: 10 × 25.00 × 0.90 = 225.00; Line2: 5 × 49.99 = 249.95 → total = 474.95
        var saved = await _svc.GetSalesOrder(so.SoNbr);
        saved!.SoTotalamt.Should().BeApproximately(474.95m, 0.01m);
    }

    [Fact]
    public async Task CreateSalesOrder_PersistsAllLines()
    {
        var (so, lines) = BuildOrder();

        await _svc.CreateSalesOrder(so, lines);

        var savedLines = await _svc.GetSalesOrderLines(so.SoNbr);
        savedLines.Should().HaveCount(2);
        savedLines.Should().Contain(l => l.SodItem == "WIDGET-100");
        savedLines.Should().Contain(l => l.SodItem == "GADGET-200");
    }

    [Fact]
    public async Task CreateSalesOrder_LinksLinesToHeaderNumber()
    {
        var (so, lines) = BuildOrder();
        so.SoNbr = string.Empty;

        await _svc.CreateSalesOrder(so, lines);

        var savedLines = await _svc.GetSalesOrderLines(so.SoNbr);
        savedLines.Should().AllSatisfy(l => l.SodNbr.Should().Be(so.SoNbr));
    }

    // ── GetSalesOrder ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesOrder_ExistingOrder_ReturnsIt()
    {
        var (so, lines) = BuildOrder("SO-FETCH-001");
        await _svc.CreateSalesOrder(so, lines);

        var fetched = await _svc.GetSalesOrder("SO-FETCH-001");

        fetched.Should().NotBeNull();
        fetched!.SoCust.Should().Be("ACME");
    }

    [Fact]
    public async Task GetSalesOrder_NonExistent_ReturnsNull()
    {
        var order = await _svc.GetSalesOrder("GHOST-ORDER");
        order.Should().BeNull();
    }

    // ── UpdateSalesOrder ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSalesOrder_ChangesFields()
    {
        var (so, lines) = BuildOrder("SO-UPD-001");
        await _svc.CreateSalesOrder(so, lines);

        so.SoNote = "Rush order";
        var result = await _svc.UpdateSalesOrder(so);

        result.Success.Should().BeTrue();
        (await _svc.GetSalesOrder("SO-UPD-001"))!.SoNote.Should().Be("Rush order");
    }

    // ── CloseOrder ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseOrder_ChangesStatusToClosed()
    {
        var (so, lines) = BuildOrder("SO-CLOSE-001");
        await _svc.CreateSalesOrder(so, lines);

        var result = await _svc.CloseOrder("SO-CLOSE-001");

        result.Success.Should().BeTrue();
        (await _svc.GetSalesOrder("SO-CLOSE-001"))!.SoStatus.Should().Be("C");
    }

    [Fact]
    public async Task CloseOrder_NonExistent_ReturnsError()
    {
        var result = await _svc.CloseOrder("GHOST-ORDER");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    // ── GetOpenOrders ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOpenOrders_ReturnsOnlyOpenStatus()
    {
        var (so1, l1) = BuildOrder("SO-OPEN-A");
        var (so2, l2) = BuildOrder("SO-OPEN-B");
        var (so3, l3) = BuildOrder("SO-CLOSED-C");

        await _svc.CreateSalesOrder(so1, l1);
        await _svc.CreateSalesOrder(so2, l2);
        await _svc.CreateSalesOrder(so3, l3);
        await _svc.CloseOrder("SO-CLOSED-C");

        var open = await _svc.GetOpenOrders();

        open.Should().Contain(o => o.SoNbr == "SO-OPEN-A");
        open.Should().Contain(o => o.SoNbr == "SO-OPEN-B");
        open.Should().NotContain(o => o.SoNbr == "SO-CLOSED-C");
    }

    [Fact]
    public async Task GetOpenOrders_FiltersByCustomer()
    {
        var (so1, l1) = BuildOrder("SO-CUST-ACME",   cust: "ACME");
        var (so2, l2) = BuildOrder("SO-CUST-GLOBEX", cust: "GLOBEX");
        await _svc.CreateSalesOrder(so1, l1);
        await _svc.CreateSalesOrder(so2, l2);

        var acmeOrders = await _svc.GetOpenOrders("ACME");

        acmeOrders.Should().OnlyContain(o => o.SoCust == "ACME");
        acmeOrders.Should().NotContain(o => o.SoNbr == "SO-CUST-GLOBEX");
    }

    // ── Customer CRUD ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomer_ExistingCustomer_ReturnsIt()
    {
        var cust = await _svc.GetCustomer("ACME");

        cust.Should().NotBeNull();
        cust!.CmName.Should().Be("Acme Corporation");
    }

    [Fact]
    public async Task GetCustomer_NonExistent_ReturnsNull()
    {
        (await _svc.GetCustomer("NOBODY")).Should().BeNull();
    }

    [Fact]
    public async Task AddCustomer_NewCustomer_Succeeds()
    {
        var cust = new CmMstr { CmCode = "NEWCO", CmName = "New Company Inc", CmStatus = "A" };

        var result = await _svc.AddCustomer(cust);

        result.Success.Should().BeTrue();
        cust.CmCrtdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
        (await _svc.GetCustomer("NEWCO")).Should().NotBeNull();
    }

    [Fact]
    public async Task AddCustomer_Duplicate_ReturnsError()
    {
        var dup = new CmMstr { CmCode = "ACME", CmName = "Duplicate Acme", CmStatus = "A" };

        var result = await _svc.AddCustomer(dup);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task UpdateCustomer_ChangesName()
    {
        var cust = await _svc.GetCustomer("ACME");
        cust!.CmName = "Acme Corp (Renamed)";

        var result = await _svc.UpdateCustomer(cust);

        result.Success.Should().BeTrue();
        (await _svc.GetCustomer("ACME"))!.CmName.Should().Be("Acme Corp (Renamed)");
    }

    [Fact]
    public async Task SearchCustomers_ByName_ReturnsMatches()
    {
        var results = await _svc.SearchCustomers("Acme");

        results.Should().HaveCount(1);
        results.Single().CmCode.Should().Be("ACME");
    }

    [Fact]
    public async Task SearchCustomers_ByCode_ReturnsMatch()
    {
        var results = await _svc.SearchCustomers("GLOBEX");
        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchCustomers_NoMatch_ReturnsEmpty()
    {
        var results = await _svc.SearchCustomers("ZZNOTFOUND");
        results.Should().BeEmpty();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static (SoMstr so, List<SodDet> lines) BuildOrder(
        string nbr  = "SO-TEST-001",
        string cust = "ACME")
    {
        var so = new SoMstr
        {
            SoNbr  = nbr,
            SoCust = cust,
            SoSite = "DEFAULT",
            SoCurr = "USD"
        };

        var lines = new List<SodDet>
        {
            new() { SodLine = 10, SodItem = "WIDGET-100", SodQty = 10m, SodPrice = 25.00m, SodDisc = 0, SodStatus = "O" },
            new() { SodLine = 20, SodItem = "GADGET-200", SodQty = 5m,  SodPrice = 49.99m, SodDisc = 0, SodStatus = "O" }
        };

        return (so, lines);
    }
}
