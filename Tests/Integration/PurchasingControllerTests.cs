using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Purchasing;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

/// <summary>
/// Tests the inline PO creation logic in PurchasingController, including
/// line numbering, total calculation, counter assignment, and status close.
/// </summary>
public class PurchasingControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly PurchasingController _ctrl;

    public PurchasingControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetNextDocumentNumber("PO")).ReturnsAsync("PO-000001");
        _appMock.Setup(a => a.GetSite()).Returns("DEFAULT");

        _ctrl = new PurchasingController(_db, _appMock.Object, NullLogger<PurchasingController>.Instance);
        SetUser(_ctrl, "testuser");
    }

    public void Dispose() => _db.Dispose();

    // ── CreatePurchaseOrder ────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePurchaseOrder_ValidRequest_Returns200()
    {
        var result = await _ctrl.CreatePurchaseOrder(BuildPoRequest());
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreatePurchaseOrder_AssignsCounterNumber_WhenPoNbrEmpty()
    {
        var req = BuildPoRequest();
        req.Header.PoNbr = string.Empty;

        await _ctrl.CreatePurchaseOrder(req);

        _appMock.Verify(a => a.GetNextDocumentNumber("PO"), Times.Once);
        req.Header.PoNbr.Should().Be("PO-000001");
    }

    [Fact]
    public async Task CreatePurchaseOrder_PreservesExplicitPoNbr()
    {
        var req = BuildPoRequest("PO-EXPLICIT");

        await _ctrl.CreatePurchaseOrder(req);

        req.Header.PoNbr.Should().Be("PO-EXPLICIT");
        _appMock.Verify(a => a.GetNextDocumentNumber(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreatePurchaseOrder_SetsStatusOpen()
    {
        var req = BuildPoRequest("PO-STATUS");
        await _ctrl.CreatePurchaseOrder(req);

        _db.PoMstr.Find("PO-STATUS")!.PoStatus.Should().Be("O");
    }

    [Fact]
    public async Task CreatePurchaseOrder_SetsEntryDateToToday()
    {
        var req = BuildPoRequest("PO-DATE");
        await _ctrl.CreatePurchaseOrder(req);

        _db.PoMstr.Find("PO-DATE")!.PoEntdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task CreatePurchaseOrder_AutonumbersLines_StartingAt10()
    {
        var req = BuildPoRequest("PO-LINES");
        foreach (var l in req.Lines) l.PodLine = 0;

        await _ctrl.CreatePurchaseOrder(req);

        var lines = _db.PodMstr.Where(l => l.PodNbr == "PO-LINES").OrderBy(l => l.PodLine).ToList();
        lines[0].PodLine.Should().Be(10);
        lines[1].PodLine.Should().Be(20);
    }

    [Fact]
    public async Task CreatePurchaseOrder_CalculatesTotalAmount()
    {
        // 10 × $15.00 + 5 × $30.00 = $300.00
        var req = BuildPoRequest("PO-TOTAL");
        await _ctrl.CreatePurchaseOrder(req);

        _db.PoMstr.Find("PO-TOTAL")!.PoTotalamt.Should().Be(300.00m);
    }

    [Fact]
    public async Task CreatePurchaseOrder_SavesAddressWhenProvided()
    {
        var req = BuildPoRequest("PO-ADDR");
        req = req with { Addr = new PoAddr { PoaNbr = "", PoaName = "ACME Warehouse", PoaCity = "Houston" } };

        await _ctrl.CreatePurchaseOrder(req);

        _db.PoAddr.SingleOrDefault(a => a.PoaNbr == "PO-ADDR").Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePurchaseOrder_PersistsAllLines()
    {
        var req = BuildPoRequest("PO-PERSIST");
        await _ctrl.CreatePurchaseOrder(req);

        _db.PodMstr.Count(l => l.PodNbr == "PO-PERSIST").Should().Be(2);
    }

    // ── GetPurchaseOrder ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPurchaseOrder_ExistingOrder_Returns200WithHeaderAndLines()
    {
        await _ctrl.CreatePurchaseOrder(BuildPoRequest("PO-GET"));

        var result = _ctrl.GetPurchaseOrder("PO-GET") as OkObjectResult;

        result.Should().NotBeNull();
        var body = result!.Value as dynamic;
        ((object)body!.header).Should().NotBeNull();
    }

    [Fact]
    public void GetPurchaseOrder_NonExistent_Returns404()
    {
        _ctrl.GetPurchaseOrder("GHOST-PO").Should().BeOfType<NotFoundResult>();
    }

    // ── GetPurchaseOrders (browse) ─────────────────────────────────────────────

    [Fact]
    public async Task GetPurchaseOrders_FiltersByVendor()
    {
        await _ctrl.CreatePurchaseOrder(BuildPoRequest("PO-V1", vend: "VENDOR-A"));
        await _ctrl.CreatePurchaseOrder(BuildPoRequest("PO-V2", vend: "VENDOR-B"));

        var result = _ctrl.GetPurchaseOrders(vend: "VENDOR-A") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    [Fact]
    public async Task GetPurchaseOrders_FiltersByStatus()
    {
        await _ctrl.CreatePurchaseOrder(BuildPoRequest("PO-OPEN1"));
        await _ctrl.ClosePO("PO-OPEN1");
        await _ctrl.CreatePurchaseOrder(BuildPoRequest("PO-OPEN2"));

        var result = _ctrl.GetPurchaseOrders(status: "O") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    [Fact]
    public void GetPurchaseOrders_Pagination_RespectsPageSize()
    {
        // Seed 5 POs directly
        for (int i = 1; i <= 5; i++)
            _db.PoMstr.Add(new PoMstr { PoNbr = $"PO-PAGE-{i}", PoVend = "VEND", PoSite = "DEFAULT", PoStatus = "O", PoEntdate = "2026-03-01" });
        _db.SaveChanges();

        var result = _ctrl.GetPurchaseOrders(pageSize: 2) as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(5);
        var orders = (System.Collections.IList)body!.orders;
        orders.Count.Should().Be(2);
    }

    // ── ClosePO ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClosePO_ChangesStatusToClosed()
    {
        await _ctrl.CreatePurchaseOrder(BuildPoRequest("PO-CLOSE"));

        var result = await _ctrl.ClosePO("PO-CLOSE");

        result.Should().BeOfType<OkObjectResult>();
        _db.PoMstr.Find("PO-CLOSE")!.PoStatus.Should().Be("C");
    }

    [Fact]
    public async Task ClosePO_NonExistent_Returns404()
    {
        var result = await _ctrl.ClosePO("GHOST-PO");
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── UpdatePurchaseOrder ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePurchaseOrder_NumberMismatch_Returns400()
    {
        var po = new PoMstr { PoNbr = "PO-MISMATCH" };
        var result = await _ctrl.UpdatePurchaseOrder("DIFFERENT", po);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CreatePoRequest BuildPoRequest(string nbr = "PO-TEST-001", string vend = "VENDOR-A")
    {
        var header = new PoMstr { PoNbr = nbr, PoVend = vend, PoSite = "DEFAULT", PoCurr = "USD" };
        var lines = new List<PodMstr>
        {
            new() { PodLine = 10, PodItem = "WIDGET-100", PodQty = 10m, PodPrice = 15.00m, PodUom = "EA", PodStatus = "O" },
            new() { PodLine = 20, PodItem = "GADGET-200", PodQty = 5m,  PodPrice = 30.00m, PodUom = "EA", PodStatus = "O" }
        };
        return new CreatePoRequest(header, lines, null);
    }

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
