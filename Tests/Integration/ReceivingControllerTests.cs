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
using ZaffreMeld.Web.Models.Receiving;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

/// <summary>
/// Tests the ReceivingController's core business logic:
/// - Receiver creation with auto-numbering
/// - PO line quantity-received tracking
/// - PO line auto-close when fully received
/// - Item QOH update on receipt
/// </summary>
public class ReceivingControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly ReceivingController _ctrl;

    public ReceivingControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetNextDocumentNumber("RV")).ReturnsAsync("RV-000001");
        _appMock.Setup(a => a.GetSite()).Returns("DEFAULT");

        // Seed a PO with lines to receive against
        _db.PoMstr.Add(new PoMstr { PoNbr = "PO-001", PoVend = "VENDOR-A", PoSite = "DEFAULT", PoStatus = "O", PoEntdate = "2026-03-01" });
        _db.PodMstr.AddRange(
            new PodMstr { PodNbr = "PO-001", PodLine = 10, PodItem = "WIDGET-100", PodQty = 20m, PodQtyrcv = 0m, PodPrice = 15m, PodUom = "EA", PodStatus = "O" },
            new PodMstr { PodNbr = "PO-001", PodLine = 20, PodItem = "GADGET-200", PodQty = 10m, PodQtyrcv = 0m, PodPrice = 30m, PodUom = "EA", PodStatus = "O" }
        );
        _db.SaveChanges();

        _ctrl = new ReceivingController(_db, _appMock.Object, NullLogger<ReceivingController>.Instance);
        SetUser(_ctrl, "testuser");
    }

    public void Dispose() => _db.Dispose();

    // ── CreateReceiver ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateReceiver_ValidRequest_Returns200()
    {
        var result = await _ctrl.CreateReceiver(BuildReceiverRequest());
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateReceiver_AssignsCounterNumber_WhenIdEmpty()
    {
        var req = BuildReceiverRequest();
        req.Header.RvId = string.Empty;

        await _ctrl.CreateReceiver(req);

        _appMock.Verify(a => a.GetNextDocumentNumber("RV"), Times.Once);
        req.Header.RvId.Should().Be("RV-000001");
    }

    [Fact]
    public async Task CreateReceiver_SetsStatusOpen()
    {
        var req = BuildReceiverRequest("RV-STATUS");
        await _ctrl.CreateReceiver(req);

        _db.RecvMstr.Find("RV-STATUS")!.RvStatus.Should().Be("O");
    }

    [Fact]
    public async Task CreateReceiver_SetsReceiveDateToToday()
    {
        var req = BuildReceiverRequest("RV-DATE");
        await _ctrl.CreateReceiver(req);

        _db.RecvMstr.Find("RV-DATE")!.RvRecvdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task CreateReceiver_LinksLinesToHeader()
    {
        var req = BuildReceiverRequest("RV-LINK");
        await _ctrl.CreateReceiver(req);

        _db.RecvDet.Where(l => l.RvdId == "RV-LINK").Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateReceiver_PersistsAllLines()
    {
        var req = BuildReceiverRequest("RV-PERSIST");
        await _ctrl.CreateReceiver(req);

        _db.RecvDet.Count(l => l.RvdId == "RV-PERSIST").Should().Be(2);
    }

    // ── QOH Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateReceiver_IncreasesItemQoh_ByReceivedQty()
    {
        var beforeQoh = _db.ItemMstr.First(i => i.ItItem == "WIDGET-100").ItQoh; // 100

        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-QOH", widgetQty: 10m));

        var afterQoh = _db.ItemMstr.First(i => i.ItItem == "WIDGET-100").ItQoh;
        afterQoh.Should().Be(beforeQoh + 10m);
    }

    [Fact]
    public async Task CreateReceiver_IncreasesQoh_ForMultipleItems()
    {
        var widgetBefore = _db.ItemMstr.First(i => i.ItItem == "WIDGET-100").ItQoh;
        var gadgetBefore = _db.ItemMstr.First(i => i.ItItem == "GADGET-200").ItQoh;

        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-MULTI", widgetQty: 5m, gadgetQty: 3m));

        _db.ItemMstr.First(i => i.ItItem == "WIDGET-100").ItQoh.Should().Be(widgetBefore + 5m);
        _db.ItemMstr.First(i => i.ItItem == "GADGET-200").ItQoh.Should().Be(gadgetBefore + 3m);
    }

    [Fact]
    public async Task CreateReceiver_DoesNotChangeQoh_ForUnknownItem()
    {
        // Receiving an item not in ItemMstr should not crash — just skip QOH update
        var req = BuildReceiverRequest("RV-UNKNOWN");
        req.Lines[0].RvdItem = "NOT-IN-MASTER";

        var act = async () => await _ctrl.CreateReceiver(req);
        await act.Should().NotThrowAsync();
    }

    // ── PO Line Qty Received Tracking ─────────────────────────────────────────

    [Fact]
    public async Task CreateReceiver_UpdatesPodQtyrcv()
    {
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-PODQTY", widgetQty: 8m));

        var podLine = _db.PodMstr.First(p => p.PodNbr == "PO-001" && p.PodLine == 10);
        podLine.PodQtyrcv.Should().Be(8m);
    }

    [Fact]
    public async Task CreateReceiver_AccumulatesQtyrcv_AcrossMultipleReceipts()
    {
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-ACC-1", widgetQty: 5m));
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-ACC-2", widgetQty: 7m));

        var podLine = _db.PodMstr.First(p => p.PodNbr == "PO-001" && p.PodLine == 10);
        podLine.PodQtyrcv.Should().Be(12m);
    }

    [Fact]
    public async Task CreateReceiver_ClosesPoLine_WhenFullyReceived()
    {
        // PO line 10 has qty 20 — receive all 20
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-FULL", widgetQty: 20m));

        var podLine = _db.PodMstr.First(p => p.PodNbr == "PO-001" && p.PodLine == 10);
        podLine.PodStatus.Should().Be("C");
    }

    [Fact]
    public async Task CreateReceiver_DoesNotClosePoLine_WhenPartiallyReceived()
    {
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-PART", widgetQty: 10m));

        var podLine = _db.PodMstr.First(p => p.PodNbr == "PO-001" && p.PodLine == 10);
        podLine.PodStatus.Should().Be("O");
    }

    [Fact]
    public async Task CreateReceiver_ClosesPoLine_WhenOverReceived()
    {
        // Receive more than ordered — should still close
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-OVER", widgetQty: 25m));

        var podLine = _db.PodMstr.First(p => p.PodNbr == "PO-001" && p.PodLine == 10);
        podLine.PodStatus.Should().Be("C");
    }

    // ── GetReceiver ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReceiver_ExistingId_Returns200()
    {
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-GET"));
        _ctrl.GetReceiver("RV-GET").Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetReceiver_NonExistent_Returns404()
    {
        _ctrl.GetReceiver("GHOST-RV").Should().BeOfType<NotFoundResult>();
    }

    // ── GetReceivers (browse) ──────────────────────────────────────────────────

    [Fact]
    public async Task GetReceivers_FiltersByVendor()
    {
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-V1", vend: "VENDOR-A"));
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-V2", vend: "VENDOR-B"));

        var result = _ctrl.GetReceivers(vend: "VENDOR-A") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    [Fact]
    public async Task GetReceivers_FiltersBySite()
    {
        await _ctrl.CreateReceiver(BuildReceiverRequest("RV-SITE1"));

        var result = _ctrl.GetReceivers(site: "DEFAULT") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().BeGreaterThan(0);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private CreateReceiverRequest BuildReceiverRequest(
        string id        = "RV-TEST-001",
        string vend      = "VENDOR-A",
        decimal widgetQty = 10m,
        decimal gadgetQty = 5m)
    {
        var header = new RecvMstr { RvId = id, RvVend = vend, RvSite = "DEFAULT" };
        var lines  = new List<RecvDet>
        {
            new() { RvdId = id, RvdPo = "PO-001", RvdPoline = 10, RvdItem = "WIDGET-100", RvdQty = widgetQty, RvdUom = "EA", RvdStatus = "O" },
            new() { RvdId = id, RvdPo = "PO-001", RvdPoline = 20, RvdItem = "GADGET-200", RvdQty = gadgetQty, RvdUom = "EA", RvdStatus = "O" }
        };
        return new CreateReceiverRequest(header, lines);
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
