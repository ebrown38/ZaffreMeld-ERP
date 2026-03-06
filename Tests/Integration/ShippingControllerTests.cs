using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Shipping;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

public class ShippingControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly ShippingController _ctrl;

    public ShippingControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetNextDocumentNumber("SH")).ReturnsAsync("SH-000001");
        _appMock.Setup(a => a.GetSite()).Returns("DEFAULT");

        _ctrl = new ShippingController(_db, _appMock.Object, NullLogger<ShippingController>.Instance);
        SetUser(_ctrl, "testuser");
    }

    public void Dispose() => _db.Dispose();

    // ── CreateShipper ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateShipper_ValidRequest_Returns200()
    {
        var result = await _ctrl.CreateShipper(BuildShipperRequest());
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateShipper_AssignsCounterNumber_WhenIdEmpty()
    {
        var req = BuildShipperRequest();
        req.Header.ShId = string.Empty;

        await _ctrl.CreateShipper(req);

        _appMock.Verify(a => a.GetNextDocumentNumber("SH"), Times.Once);
        req.Header.ShId.Should().Be("SH-000001");
    }

    [Fact]
    public async Task CreateShipper_SetsStatusOpen()
    {
        var req = BuildShipperRequest("SH-STATUS");
        await _ctrl.CreateShipper(req);

        _db.ShipMstr.Find("SH-STATUS")!.ShStatus.Should().Be("O");
    }

    [Fact]
    public async Task CreateShipper_SetsEntryDateToToday()
    {
        var req = BuildShipperRequest("SH-DATE");
        await _ctrl.CreateShipper(req);

        _db.ShipMstr.Find("SH-DATE")!.ShEntdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task CreateShipper_AutonumbersLines_SequentiallyFrom1()
    {
        var req = BuildShipperRequest("SH-LINES");
        foreach (var l in req.Lines) l.ShdLine = 0;

        await _ctrl.CreateShipper(req);

        var lines = _db.ShipDet.Where(l => l.ShdId == "SH-LINES").OrderBy(l => l.ShdLine).ToList();
        lines[0].ShdLine.Should().Be(1);
        lines[1].ShdLine.Should().Be(2);
    }

    [Fact]
    public async Task CreateShipper_PersistsAllLines()
    {
        var req = BuildShipperRequest("SH-PERSIST");
        await _ctrl.CreateShipper(req);

        _db.ShipDet.Count(l => l.ShdId == "SH-PERSIST").Should().Be(2);
    }

    [Fact]
    public async Task CreateShipper_LinksLinesToHeader()
    {
        var req = BuildShipperRequest();
        req.Header.ShId = string.Empty;

        await _ctrl.CreateShipper(req);

        _db.ShipDet.Should().OnlyContain(l => l.ShdId == req.Header.ShId);
    }

    // ── GetShipper ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetShipper_ExistingId_Returns200()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-GET"));
        _ctrl.GetShipper("SH-GET").Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetShipper_NonExistent_Returns404()
    {
        _ctrl.GetShipper("GHOST").Should().BeOfType<NotFoundResult>();
    }

    // ── ConfirmShipment ────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmShipment_ChangesStatusToClosed()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-CONFIRM"));

        await _ctrl.ConfirmShipment("SH-CONFIRM", new ConfirmShipmentRequest("2026-03-04", "TRACK-999"));

        var sh = _db.ShipMstr.Find("SH-CONFIRM")!;
        sh.ShStatus.Should().Be("C");
        sh.ShPosted.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmShipment_SetsShipDate()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-DATE2"));

        await _ctrl.ConfirmShipment("SH-DATE2", new ConfirmShipmentRequest("2026-03-10", null));

        _db.ShipMstr.Find("SH-DATE2")!.ShShipdate.Should().Be("2026-03-10");
    }

    [Fact]
    public async Task ConfirmShipment_UsesTodayWhenDateNull()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-TODAY"));

        await _ctrl.ConfirmShipment("SH-TODAY", new ConfirmShipmentRequest(null, null));

        _db.ShipMstr.Find("SH-TODAY")!.ShShipdate
            .Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task ConfirmShipment_SetsTrackingNumber()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-TRACK"));

        await _ctrl.ConfirmShipment("SH-TRACK", new ConfirmShipmentRequest(null, "FEDEX-99999"));

        _db.ShipMstr.Find("SH-TRACK")!.ShTrackno.Should().Be("FEDEX-99999");
    }

    [Fact]
    public async Task ConfirmShipment_AlreadyPosted_Returns400()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-DUPE"));
        await _ctrl.ConfirmShipment("SH-DUPE", new ConfirmShipmentRequest(null, null));

        // Second confirm attempt
        var result = await _ctrl.ConfirmShipment("SH-DUPE", new ConfirmShipmentRequest(null, null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ConfirmShipment_NonExistent_Returns404()
    {
        var result = await _ctrl.ConfirmShipment("GHOST", new ConfirmShipmentRequest(null, null));
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── GetShippers (browse) ───────────────────────────────────────────────────

    [Fact]
    public async Task GetShippers_FiltersByCustomer()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-C1", cust: "ACME"));
        await _ctrl.CreateShipper(BuildShipperRequest("SH-C2", cust: "GLOBEX"));

        var result = _ctrl.GetShippers(cust: "ACME") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    [Fact]
    public async Task GetShippers_FiltersByStatus()
    {
        await _ctrl.CreateShipper(BuildShipperRequest("SH-S1"));
        await _ctrl.ConfirmShipment("SH-S1", new ConfirmShipmentRequest(null, null));
        await _ctrl.CreateShipper(BuildShipperRequest("SH-S2"));

        var result = _ctrl.GetShippers(status: "O") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CreateShipperRequest BuildShipperRequest(string id = "SH-TEST-001", string cust = "ACME")
    {
        var header = new ShipMstr { ShId = id, ShCust = cust, ShSite = "DEFAULT", ShCarrier = "FEDEX" };
        var lines  = new List<ShipDet>
        {
            new() { ShdLine = 1, ShdItem = "WIDGET-100", ShdSo = "SO-001", ShdQty = 10, ShdUom = "EA" },
            new() { ShdLine = 2, ShdItem = "GADGET-200", ShdSo = "SO-001", ShdQty = 5,  ShdUom = "EA" }
        };
        return new CreateShipperRequest(header, lines);
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
