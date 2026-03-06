using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Services;
using ZaffreMeld.Web.Services.EDI;
using System.Security.Claims;

// EdiMapResult is a separate record in ZaffreMeld.Web.Services.EDI — used by all IEdiService methods.
// ServiceResult is used only by non-EDI services.

namespace ZaffreMeld.Tests.Integration;

/// <summary>
/// Tests for EdiApiController — verifies routing, request validation,
/// and correct delegation to IEdiService methods.
/// </summary>
public class EdiApiControllerTests
{
    private readonly Mock<IEdiService> _ediMock;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly EdiApiController _ctrl;

    private static readonly EdpPartner AcmePartner = new()
    {
        EdpId   = "ACME",
        EdpDesc = "Acme Corporation",
        EdpIsa  = "ACMEPARTNER",
        EdpGs   = "ACMEPARTNER",
        EdpType = "customer",
        EdpSite = "DEFAULT",
        EdpActive = true
    };

    public EdiApiControllerTests()
    {
        _ediMock = new Mock<IEdiService>();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetSite()).Returns("DEFAULT");

        // Default setups
        _ediMock.Setup(e => e.ProcessInbound(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(EdiMapResult.Ok(null, "850 processed."));
        _ediMock.Setup(e => e.ProcessInboundFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(EdiMapResult.Ok(null, "File processed."));
        _ediMock.Setup(e => e.Generate810(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(EdiMapResult.Ok(new { X12 = "ISA*..." }, "810 generated."));
        _ediMock.Setup(e => e.Generate856(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(EdiMapResult.Ok(new { X12 = "ISA*..." }, "856 generated."));
        _ediMock.Setup(e => e.Generate997(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync("ISA*...*997...");
        _ediMock.Setup(e => e.GetHistory(null, null, null, 200))
            .ReturnsAsync(new List<EdiMstr>());
        _ediMock.Setup(e => e.GetTransaction(It.IsAny<int>()))
            .ReturnsAsync((EdiMstr?)null);
        _ediMock.Setup(e => e.GetPartners(It.IsAny<bool>()))
            .ReturnsAsync(new List<EdpPartner> { AcmePartner });
        _ediMock.Setup(e => e.GetPartner("ACME"))
            .ReturnsAsync(AcmePartner);
        _ediMock.Setup(e => e.GetPartner("GHOST"))
            .ReturnsAsync((EdpPartner?)null);
        _ediMock.Setup(e => e.SavePartner(It.IsAny<EdpPartner>()))
            .ReturnsAsync(EdiMapResult.Ok(null, "Partner saved."));
        _ediMock.Setup(e => e.GetXrefs(It.IsAny<string>()))
            .ReturnsAsync(new List<EdiXref>());
        _ediMock.Setup(e => e.SaveXref(It.IsAny<EdiXref>()))
            .ReturnsAsync(EdiMapResult.Ok(null, "Xref saved."));
        _ediMock.Setup(e => e.DeleteXref(It.IsAny<int>()))
            .ReturnsAsync(EdiMapResult.Ok(null, "Xref deleted."));
        _ediMock.Setup(e => e.GetDocDefs(It.IsAny<string?>()))
            .ReturnsAsync(new List<EdiDoc>());
        _ediMock.Setup(e => e.SaveDocDef(It.IsAny<EdiDoc>()))
            .ReturnsAsync(EdiMapResult.Ok(null, "Doc def saved."));

        _ctrl = new EdiApiController(_ediMock.Object, _appMock.Object, NullLogger<EdiApiController>.Instance);
        SetUser(_ctrl, "ediuser");
    }

    // ── Inbound ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostInbound_ValidRaw_Returns200()
    {
        var req    = new InboundEdiRequest(SampleEdi.Edi850, "ACME", "DEFAULT");
        var result = await _ctrl.PostInbound(req);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PostInbound_EmptyRaw_Returns400()
    {
        var req    = new InboundEdiRequest("", "ACME", "DEFAULT");
        var result = await _ctrl.PostInbound(req);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostInbound_NullRaw_Returns400()
    {
        var req    = new InboundEdiRequest(null!, "ACME", "DEFAULT");
        var result = await _ctrl.PostInbound(req);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostInbound_ServiceFails_Returns422()
    {
        _ediMock.Setup(e => e.ProcessInbound(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(EdiMapResult.Error("Parse error."));

        var req    = new InboundEdiRequest(SampleEdi.Edi850, "ACME", "DEFAULT");
        var result = await _ctrl.PostInbound(req);
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task PostInbound_NullSite_UsesAppSite()
    {
        var req = new InboundEdiRequest(SampleEdi.Edi850, "ACME", null);
        await _ctrl.PostInbound(req);

        _appMock.Verify(a => a.GetSite(), Times.Once);
        _ediMock.Verify(e => e.ProcessInbound(SampleEdi.Edi850, "ACME", "DEFAULT"), Times.Once);
    }

    [Fact]
    public async Task PostInbound_WithSite_DoesNotCallAppGetSite()
    {
        var req = new InboundEdiRequest(SampleEdi.Edi850, "ACME", "WEST");
        await _ctrl.PostInbound(req);

        _appMock.Verify(a => a.GetSite(), Times.Never);
        _ediMock.Verify(e => e.ProcessInbound(SampleEdi.Edi850, "ACME", "WEST"), Times.Once);
    }

    [Fact]
    public async Task PostInboundFile_ValidPath_Returns200()
    {
        var req    = new InboundFileRequest("/edi/in/acme/po.edi", "ACME", "DEFAULT");
        var result = await _ctrl.PostInboundFile(req);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PostInboundFile_ServiceFails_Returns422()
    {
        _ediMock.Setup(e => e.ProcessInboundFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(EdiMapResult.Error("File not found."));

        var req    = new InboundFileRequest("/ghost.edi", "ACME", "DEFAULT");
        var result = await _ctrl.PostInboundFile(req);
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── Outbound 810 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate810_WithPartner_Returns200()
    {
        (await _ctrl.Generate810("INV-001", "ACME")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Generate810_EmptyPartner_Returns400()
    {
        (await _ctrl.Generate810("INV-001", "")).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Generate810_ServiceFails_Returns404()
    {
        _ediMock.Setup(e => e.Generate810("GHOST", "ACME"))
            .ReturnsAsync(EdiMapResult.Error("Invoice not found."));

        (await _ctrl.Generate810("GHOST", "ACME")).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Generate810_CallsServiceWithCorrectArgs()
    {
        await _ctrl.Generate810("INV-001", "ACME");
        _ediMock.Verify(e => e.Generate810("INV-001", "ACME"), Times.Once);
    }

    // ── Outbound 856 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate856_WithPartner_Returns200()
    {
        (await _ctrl.Generate856("SH-001", "ACME")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Generate856_EmptyPartner_Returns400()
    {
        (await _ctrl.Generate856("SH-001", "")).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Generate856_ServiceFails_Returns404()
    {
        _ediMock.Setup(e => e.Generate856("GHOST", "ACME"))
            .ReturnsAsync(EdiMapResult.Error("Shipper not found."));

        (await _ctrl.Generate856("GHOST", "ACME")).Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Outbound 997 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate997_ValidRequest_Returns200WithX12()
    {
        var req    = new Ack997Request(SampleEdi.Edi850, "ACME", "A", null);
        var result = await _ctrl.Generate997(req) as OkObjectResult;

        result.Should().NotBeNull();
        var body = result!.Value as dynamic;
        ((string)body!.X12).Should().Contain("ISA");
    }

    [Fact]
    public async Task Generate997_PassesAckCodeToService()
    {
        var req = new Ack997Request(SampleEdi.Edi850, "ACME", "R", "Mapping failed");
        await _ctrl.Generate997(req);

        _ediMock.Verify(e => e.Generate997(SampleEdi.Edi850, "R", "ACME", "Mapping failed"), Times.Once);
    }

    // ── History ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_NoFilter_CallsServiceAndReturns200()
    {
        _ediMock.Setup(e => e.GetHistory(null, null, null, 200))
            .ReturnsAsync(new List<EdiMstr> { new() { EdiId = "TX-001", EdiDoc2 = "850", EdiPartner = "ACME", EdiDir = "IN", EdiStatus = "A", EdiSite = "DEFAULT" } });

        var result = await _ctrl.GetHistory() as OkObjectResult;
        ((List<EdiMstr>)result!.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetHistory_PassesFiltersToService()
    {
        _ediMock.Setup(e => e.GetHistory("ACME", "850", "IN", 50))
            .ReturnsAsync(new List<EdiMstr>());

        await _ctrl.GetHistory(partner: "ACME", docType: "850", dir: "IN", max: 50);

        _ediMock.Verify(e => e.GetHistory("ACME", "850", "IN", 50), Times.Once);
    }

    [Fact]
    public async Task GetTransaction_NonExistent_Returns404()
    {
        (await _ctrl.GetTransaction(9999)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetTransaction_Existing_Returns200()
    {
        _ediMock.Setup(e => e.GetTransaction(1))
            .ReturnsAsync(new EdiMstr { EdiId = "TX-001", EdiDoc2 = "850", EdiPartner = "ACME", EdiDir = "IN", EdiStatus = "A", EdiSite = "DEFAULT" });

        (await _ctrl.GetTransaction(1)).Should().BeOfType<OkObjectResult>();
    }

    // ── Partners ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPartners_Returns200WithList()
    {
        var result = await _ctrl.GetPartners() as OkObjectResult;
        ((List<EdpPartner>)result!.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPartners_PassesActiveOnlyFlag()
    {
        await _ctrl.GetPartners(activeOnly: false);
        _ediMock.Verify(e => e.GetPartners(false), Times.Once);
    }

    [Fact]
    public async Task GetPartner_ExistingId_Returns200()
    {
        (await _ctrl.GetPartner("ACME")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPartner_NonExistent_Returns404()
    {
        (await _ctrl.GetPartner("GHOST")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SavePartner_Success_Returns200()
    {
        var result = await _ctrl.SavePartner(Clone(AcmePartner));
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SavePartner_CoalescesNullStrings_BeforeCallingService()
    {
        EdpPartner? captured = null;
        _ediMock.Setup(e => e.SavePartner(It.IsAny<EdpPartner>()))
            .Callback<EdpPartner>(p => captured = p)
            .ReturnsAsync(EdiMapResult.Ok(null, "saved."));

        var partner = new EdpPartner { EdpId = null!, EdpDesc = null! };
        await _ctrl.SavePartner(partner);

        captured!.EdpId.Should().Be(string.Empty);
        captured.EdpDesc.Should().Be(string.Empty);
    }

    [Fact]
    public async Task SavePartner_ServiceFails_Returns400()
    {
        _ediMock.Setup(e => e.SavePartner(It.IsAny<EdpPartner>()))
            .ReturnsAsync(EdiMapResult.Error("Duplicate partner ID."));

        (await _ctrl.SavePartner(Clone(AcmePartner))).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Xrefs ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetXrefs_NoPartner_Returns200()
    {
        (await _ctrl.GetXrefs()).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetXrefs_PassesPartnerFilter()
    {
        await _ctrl.GetXrefs(partner: "ACME");
        _ediMock.Verify(e => e.GetXrefs("ACME"), Times.Once);
    }

    [Fact]
    public async Task SaveXref_Success_Returns200()
    {
        var xref   = new EdiXref { ExrTpaddr = "ACME", ExrBsaddr = "ACME-CUST", ExrType = "customer", ExrActive = true };
        var result = await _ctrl.SaveXref(xref);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SaveXref_CoalescesNullStrings()
    {
        EdiXref? captured = null;
        _ediMock.Setup(e => e.SaveXref(It.IsAny<EdiXref>()))
            .Callback<EdiXref>(x => captured = x)
            .ReturnsAsync(EdiMapResult.Ok(null, "saved."));

        await _ctrl.SaveXref(new EdiXref { ExrTpaddr = null!, ExrBsaddr = null!, ExrType = null! });

        captured!.ExrTpaddr.Should().Be(string.Empty);
        captured.ExrBsaddr.Should().Be(string.Empty);
        captured.ExrType.Should().Be(string.Empty);
    }

    [Fact]
    public async Task DeleteXref_Success_Returns200()
    {
        (await _ctrl.DeleteXref(1)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteXref_ServiceFails_Returns404()
    {
        _ediMock.Setup(e => e.DeleteXref(99)).ReturnsAsync(EdiMapResult.Error("Not found."));
        (await _ctrl.DeleteXref(99)).Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Doc Defs ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDocDefs_Returns200()
    {
        (await _ctrl.GetDocDefs()).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDocDefs_PassesPartnerFilter()
    {
        await _ctrl.GetDocDefs(partner: "ACME");
        _ediMock.Verify(e => e.GetDocDefs("ACME"), Times.Once);
    }

    [Fact]
    public async Task SaveDocDef_Success_Returns200()
    {
        var doc    = new EdiDoc { EddId = "DOC-001", EddDesc = "Test", EddType = "850", EddPartner = "ACME", EddDir = "IN", EddMap = "Map850", EddActive = true };
        var result = await _ctrl.SaveDocDef(doc);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SaveDocDef_CoalescesNullStrings()
    {
        EdiDoc? captured = null;
        _ediMock.Setup(e => e.SaveDocDef(It.IsAny<EdiDoc>()))
            .Callback<EdiDoc>(d => captured = d)
            .ReturnsAsync(EdiMapResult.Ok(null, "saved."));

        await _ctrl.SaveDocDef(new EdiDoc { EddId = null!, EddDesc = null!, EddType = null!, EddPartner = null!, EddMap = null!, EddDir = null! });

        captured!.EddId.Should().Be(string.Empty);
        captured.EddType.Should().Be(string.Empty);
        captured.EddMap.Should().Be(string.Empty);
    }

    [Fact]
    public async Task SaveDocDef_ServiceFails_Returns400()
    {
        _ediMock.Setup(e => e.SaveDocDef(It.IsAny<EdiDoc>()))
            .ReturnsAsync(EdiMapResult.Error("Duplicate doc ID."));

        var doc    = new EdiDoc { EddId = "DUP", EddDesc = "", EddType = "", EddPartner = "", EddDir = "", EddMap = "" };
        var result = await _ctrl.SaveDocDef(doc);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static EdpPartner Clone(EdpPartner p) => new()
    {
        EdpId = p.EdpId, EdpDesc = p.EdpDesc, EdpIsa = p.EdpIsa, EdpGs = p.EdpGs,
        EdpType = p.EdpType, EdpSite = p.EdpSite, EdpActive = p.EdpActive,
        EdpFtpid = p.EdpFtpid, EdpAs2id = p.EdpAs2id, EdpNote = p.EdpNote
    };

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
