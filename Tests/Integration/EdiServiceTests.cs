using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Shipping;
using ZaffreMeld.Web.Services;
using ZaffreMeld.Web.Services.EDI;

namespace ZaffreMeld.Tests.Integration;

public class EdiServiceTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IOrderService> _ordersMock;
    private readonly EdiService _svc;

    public EdiServiceTests()
    {
        _db         = TestDbFactory.CreateSeeded();
        _ordersMock = new Mock<IOrderService>();

        // Default: CreateSalesOrder succeeds
        _ordersMock
            .Setup(o => o.CreateSalesOrder(It.IsAny<Web.Models.Orders.SoMstr>(), It.IsAny<List<Web.Models.Orders.SodDet>>()))
            .ReturnsAsync(ServiceResult.Ok("Sales order created.", "SO-001001"));

        _svc = new EdiService(
            _db,
            _ordersMock.Object,
            TestConfig.Create(),
            NullLogger<EdiService>.Instance);

        // Seed a trading partner
        _db.EdpPartner.Add(new EdpPartner
        {
            EdpId     = "ACME",
            EdpDesc   = "Acme Corporation",
            EdpIsa    = "ACMEPARTNER",
            EdpGs     = "ACMEPARTNER",
            EdpType   = "customer",
            EdpSite   = "DEFAULT",
            EdpActive = true
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── ProcessInbound — 850 ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessInbound_Valid850_Succeeds()
    {
        var result = await _svc.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessInbound_Valid850_CallsCreateSalesOrder()
    {
        await _svc.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT");

        _ordersMock.Verify(
            o => o.CreateSalesOrder(
                It.IsAny<Web.Models.Orders.SoMstr>(),
                It.Is<List<Web.Models.Orders.SodDet>>(l => l.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessInbound_Valid850_LogsTransaction()
    {
        await _svc.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT");

        _db.EdiMstr.Should().Contain(e => e.EdiDoc2 == "850" && e.EdiDir == "IN");
    }

    [Fact]
    public async Task ProcessInbound_Valid850_AutoGenerates997()
    {
        await _svc.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT");

        _db.EdiMstr.Should().Contain(e => e.EdiDoc2 == "997" && e.EdiDir == "OUT");
    }

    [Fact]
    public async Task ProcessInbound_Valid997_Succeeds()
    {
        var result = await _svc.ProcessInbound(SampleEdi.Edi997, "ACME", "DEFAULT");

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("997");
    }

    [Fact]
    public async Task ProcessInbound_Valid997_DoesNotCreateSalesOrder()
    {
        await _svc.ProcessInbound(SampleEdi.Edi997, "ACME", "DEFAULT");

        _ordersMock.Verify(
            o => o.CreateSalesOrder(It.IsAny<Web.Models.Orders.SoMstr>(), It.IsAny<List<Web.Models.Orders.SodDet>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessInbound_InvalidX12_ReturnsError()
    {
        var result = await _svc.ProcessInbound("not-valid-edi-at-all", "ACME", "DEFAULT");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("parse error");
    }

    [Fact]
    public async Task ProcessInbound_InvalidX12_StillLogsTransaction()
    {
        await _svc.ProcessInbound("bad input!!", "ACME", "DEFAULT");

        _db.EdiMstr.Should().Contain(e => e.EdiStatus == "E");
    }

    [Fact]
    public async Task ProcessInbound_WhenOrderServiceFails_ReturnsError()
    {
        _ordersMock
            .Setup(o => o.CreateSalesOrder(It.IsAny<Web.Models.Orders.SoMstr>(), It.IsAny<List<Web.Models.Orders.SodDet>>()))
            .ReturnsAsync(ServiceResult.Error("Database error"));

        var result = await _svc.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Database error");
    }

    [Fact]
    public async Task ProcessInbound_WithXref_MapsCustomerCode()
    {
        _db.EdiXref.Add(new EdiXref
        {
            ExrTpaddr = "ACMEPARTNER",
            ExrBsaddr = "ACME",
            ExrType   = "customer",
            ExrActive = true
        });
        await _db.SaveChangesAsync();

        Web.Models.Orders.SoMstr? capturedSo = null;
        _ordersMock
            .Setup(o => o.CreateSalesOrder(It.IsAny<Web.Models.Orders.SoMstr>(), It.IsAny<List<Web.Models.Orders.SodDet>>()))
            .Callback<Web.Models.Orders.SoMstr, List<Web.Models.Orders.SodDet>>((so, _) => capturedSo = so)
            .ReturnsAsync(ServiceResult.Ok("Created.", "SO-001001"));

        await _svc.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT");

        capturedSo!.SoCust.Should().Be("ACME");
    }

    // ── ProcessInboundFile ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessInboundFile_MissingFile_ReturnsError()
    {
        var result = await _svc.ProcessInboundFile("/nonexistent/path/file.edi", "ACME", "DEFAULT");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ProcessInboundFile_ValidFile_ProcessesIt()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, SampleEdi.Edi850);

        try
        {
            var result = await _svc.ProcessInboundFile(path, "ACME", "DEFAULT");
            result.Success.Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Generate810 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate810_ValidInvoice_Succeeds()
    {
        SeedInvoice("INV-001");

        var result = await _svc.Generate810("INV-001", "ACME");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Generate810_ProducesX12Output()
    {
        SeedInvoice("INV-002");

        var result = await _svc.Generate810("INV-002", "ACME");

        var x12 = Anon.Prop<string>(result.Data, "X12");
        x12.Should().Contain("BIG*");
    }

    [Fact]
    public async Task Generate810_LogsOutboundTransaction()
    {
        SeedInvoice("INV-003");

        await _svc.Generate810("INV-003", "ACME");

        _db.EdiMstr.Should().Contain(e => e.EdiDoc2 == "810" && e.EdiDir == "OUT");
    }

    [Fact]
    public async Task Generate810_MissingInvoice_ReturnsError()
    {
        var result = await _svc.Generate810("GHOST-INV", "ACME");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Generate810_MissingPartner_ReturnsError()
    {
        SeedInvoice("INV-004");

        var result = await _svc.Generate810("INV-004", "UNKNOWN-PARTNER");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    // ── Generate856 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate856_ValidShipper_Succeeds()
    {
        SeedShipper("SH-001");

        var result = await _svc.Generate856("SH-001", "ACME");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Generate856_ProducesX12Output()
    {
        SeedShipper("SH-002");

        var result = await _svc.Generate856("SH-002", "ACME");

        var x12 = Anon.Prop<string>(result.Data, "X12");
        x12.Should().Contain("BSN*");
    }

    [Fact]
    public async Task Generate856_LogsOutboundTransaction()
    {
        SeedShipper("SH-003");

        await _svc.Generate856("SH-003", "ACME");

        _db.EdiMstr.Should().Contain(e => e.EdiDoc2 == "856" && e.EdiDir == "OUT");
    }

    [Fact]
    public async Task Generate856_MissingShipper_ReturnsError()
    {
        var result = await _svc.Generate856("GHOST-SH", "ACME");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    // ── Generate997 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate997_ValidInput_ProducesX12()
    {
        var fa = await _svc.Generate997(SampleEdi.Edi850, "A", "ACME");

        fa.Should().Contain("ST*997*");
        fa.Should().Contain("AK5*A");
    }

    [Fact]
    public async Task Generate997_WithRejection_IncludesErrorCode()
    {
        var fa = await _svc.Generate997(SampleEdi.Edi850, "R", "ACME", "Mapping failed");

        fa.Should().Contain("AK5*R");
    }

    // ── Partner CRUD ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPartners_ReturnsActivePartners()
    {
        _db.EdpPartner.Add(new EdpPartner { EdpId = "INACTIVE", EdpDesc = "Off", EdpActive = false });
        await _db.SaveChangesAsync();

        var partners = await _svc.GetPartners(activeOnly: true);

        partners.Should().OnlyContain(p => p.EdpActive);
        partners.Should().NotContain(p => p.EdpId == "INACTIVE");
    }

    [Fact]
    public async Task GetPartners_AllIncludesInactive()
    {
        _db.EdpPartner.Add(new EdpPartner { EdpId = "INACTIVE2", EdpDesc = "Off", EdpActive = false });
        await _db.SaveChangesAsync();

        var partners = await _svc.GetPartners(activeOnly: false);

        partners.Should().Contain(p => p.EdpId == "INACTIVE2");
    }

    [Fact]
    public async Task GetPartner_ExistingId_ReturnsPartner()
    {
        var p = await _svc.GetPartner("ACME");

        p.Should().NotBeNull();
        p!.EdpDesc.Should().Be("Acme Corporation");
    }

    [Fact]
    public async Task GetPartner_NonExistent_ReturnsNull()
    {
        (await _svc.GetPartner("NOBODY")).Should().BeNull();
    }

    [Fact]
    public async Task SavePartner_NewPartner_Persists()
    {
        var partner = new EdpPartner
        {
            EdpId   = "GLOBEX",
            EdpDesc = "Globex Corp",
            EdpIsa  = "GLOBEXPARTNER",
            EdpGs   = "GLOBEXPARTNER",
            EdpType = "customer",
            EdpActive = true
        };

        var result = await _svc.SavePartner(partner);

        result.Success.Should().BeTrue();
        (await _svc.GetPartner("GLOBEX")).Should().NotBeNull();
    }

    [Fact]
    public async Task SavePartner_ExistingPartner_Updates()
    {
        var p = await _svc.GetPartner("ACME");
        p!.EdpDesc = "Acme Renamed";

        await _svc.SavePartner(p);

        (await _svc.GetPartner("ACME"))!.EdpDesc.Should().Be("Acme Renamed");
    }

    // ── Xref CRUD ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveXref_NewXref_Persists()
    {
        var xref = new EdiXref
        {
            ExrTpaddr = "ACMEPARTNER",
            ExrBsaddr = "ACME",
            ExrType   = "customer",
            ExrActive = true
        };

        var result = await _svc.SaveXref(xref);

        result.Success.Should().BeTrue();
        (await _svc.GetXrefs("ACMEPARTNER")).Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteXref_ExistingXref_Removes()
    {
        _db.EdiXref.Add(new EdiXref
        {
            ExrTpaddr = "DEL-PARTNER",
            ExrBsaddr = "DEL-CODE",
            ExrType   = "customer",
            ExrActive = true
        });
        await _db.SaveChangesAsync();
        var xrefId = _db.EdiXref.First(x => x.ExrTpaddr == "DEL-PARTNER").Id;

        var result = await _svc.DeleteXref(xrefId);

        result.Success.Should().BeTrue();
        (await _svc.GetXrefs("DEL-PARTNER")).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteXref_NonExistent_ReturnsError()
    {
        var result = await _svc.DeleteXref(99999);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    // ── GetHistory ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_FiltersByPartner()
    {
        _db.EdiMstr.AddRange(
            new EdiMstr { EdiId = "TX-1", EdiDoc2 = "850", EdiPartner = "ACME",   EdiDir = "IN",  EdiStatus = "A", EdiSite = "DEFAULT", EdiTimestamp = DateTime.UtcNow },
            new EdiMstr { EdiId = "TX-2", EdiDoc2 = "856", EdiPartner = "GLOBEX", EdiDir = "OUT", EdiStatus = "A", EdiSite = "DEFAULT", EdiTimestamp = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var history = await _svc.GetHistory(partner: "ACME");

        history.Should().OnlyContain(e => e.EdiPartner == "ACME");
    }

    [Fact]
    public async Task GetHistory_FiltersByDocType()
    {
        _db.EdiMstr.AddRange(
            new EdiMstr { EdiId = "TX-3", EdiDoc2 = "850", EdiPartner = "ACME", EdiDir = "IN",  EdiStatus = "A", EdiSite = "DEFAULT", EdiTimestamp = DateTime.UtcNow },
            new EdiMstr { EdiId = "TX-4", EdiDoc2 = "997", EdiPartner = "ACME", EdiDir = "OUT", EdiStatus = "A", EdiSite = "DEFAULT", EdiTimestamp = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var history = await _svc.GetHistory(docType: "850");

        history.Should().OnlyContain(e => e.EdiDoc2 == "850");
    }

    [Fact]
    public async Task GetHistory_RespectsMaxLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            _db.EdiMstr.Add(new EdiMstr
            {
                EdiId = $"TX-LIMIT-{i}", EdiDoc2 = "850",
                EdiPartner = "ACME", EdiDir = "IN",
                EdiStatus = "A", EdiSite = "DEFAULT",
                EdiTimestamp = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var history = await _svc.GetHistory(max: 5);

        history.Should().HaveCount(5);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SeedInvoice(string id)
    {
        _db.ArMstr.Add(new ArMstr
        {
            ArId      = id,
            ArCust    = "ACME",
            ArAmt     = 499.95m,
            ArEntdate = "2026-03-01",
            ArRef     = "PO-98765",
            ArStatus  = "O"
        });
        _db.ArdMstr.AddRange(
            new ArdMstr { ArdId = $"{id}-1", ArdNbr = id, ArdLine = 1, ArdItem = "WIDGET-100", ArdAmt = 250.00m, ArdRef = "PO-98765" },
            new ArdMstr { ArdId = $"{id}-2", ArdNbr = id, ArdLine = 2, ArdItem = "GADGET-200", ArdAmt = 249.95m, ArdRef = "PO-98765" }
        );
        _db.SaveChanges();
    }

    private void SeedShipper(string id)
    {
        _db.ShipMstr.Add(new ShipMstr
        {
            ShId       = id,
            ShStatus   = "O",
            ShSite     = "DEFAULT",
            ShShipdate = "2026-03-04",
            ShCarrier  = "FEDEX",
            ShTrackno  = "TRACK-12345",
            ShWeight   = 25.5m
        });
        _db.ShipDet.AddRange(
            new ShipDet { ShdId = id, ShdLine = 1, ShdItem = "WIDGET-100", ShdSo = "SO-001001", ShdQty = 10, ShdUom = "EA" },
            new ShipDet { ShdId = id, ShdLine = 2, ShdItem = "GADGET-200", ShdSo = "SO-001001", ShdQty = 5,  ShdUom = "EA" }
        );
        _db.SaveChanges();
    }
}