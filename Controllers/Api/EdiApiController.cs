using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Services;
using ZaffreMeld.Web.Services.EDI;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZaffreMeld.Web.Controllers.Api;

/// <summary>
/// EDI API controller.
/// Provides REST endpoints for inbound capture, outbound generation,
/// partner config, xref mapping, and transaction history.
/// </summary>
[ApiController]
[Route("api/edi")]
[Authorize(Roles = "admin,edi")]
public class EdiApiController : ControllerBase
{
    private readonly IEdiService _edi;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<EdiApiController> _logger;

    public EdiApiController(IEdiService edi, IZaffreMeldAppService app, ILogger<EdiApiController> logger)
    {
        _edi    = edi;
        _app    = app;
        _logger = logger;
    }

    // ── Inbound ────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/edi/inbound
    /// Accept a raw X12 EDI document, parse it, map it, and create the
    /// corresponding ZaffreMeld document (SO for 850, etc.).
    /// Body: { "raw": "ISA*00*...", "partner": "ACME", "site": "DEFAULT" }
    /// </summary>
    [HttpPost("inbound")]
    public async Task<IActionResult> PostInbound([FromBody] InboundEdiRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Raw))
            return BadRequest("Raw X12 body is required.");

        var site = req.Site ?? _app.GetSite();
        var result = await _edi.ProcessInbound(req.Raw, req.Partner, site);

        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    /// <summary>
    /// POST /api/edi/inbound/file
    /// Process an EDI file from the server filesystem (for FTP pickup integration).
    /// Body: { "filePath": "/edi/in/partner/file.edi", "partner": "ACME", "site": "DEFAULT" }
    /// </summary>
    [HttpPost("inbound/file")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> PostInboundFile([FromBody] InboundFileRequest req)
    {
        var result = await _edi.ProcessInboundFile(req.FilePath, req.Partner, req.Site ?? _app.GetSite());
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    // ── Outbound ───────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/edi/outbound/810/{arId}?partner=ACME
    /// Generate an X12 810 Invoice for the given AR invoice ID.
    /// </summary>
    [HttpPost("outbound/810/{arId}")]
    public async Task<IActionResult> Generate810(string arId, [FromQuery] string partner)
    {
        if (string.IsNullOrEmpty(partner)) return BadRequest("partner query param required.");
        var result = await _edi.Generate810(arId, partner);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// POST /api/edi/outbound/856/{shipId}?partner=ACME
    /// Generate an X12 856 ASN for the given shipper ID.
    /// </summary>
    [HttpPost("outbound/856/{shipId}")]
    public async Task<IActionResult> Generate856(string shipId, [FromQuery] string partner)
    {
        if (string.IsNullOrEmpty(partner)) return BadRequest("partner query param required.");
        var result = await _edi.Generate856(shipId, partner);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// POST /api/edi/outbound/997
    /// Generate a 997 FA for the provided raw inbound document.
    /// Body: { "raw": "ISA*...", "partner": "ACME", "ackCode": "A", "note": null }
    /// </summary>
    [HttpPost("outbound/997")]
    public async Task<IActionResult> Generate997([FromBody] Ack997Request req)
    {
        var fa = await _edi.Generate997(req.Raw, req.AckCode, req.Partner, req.Note);
        return Ok(new { X12 = fa });
    }

    // ── Transaction history ────────────────────────────────────────────────────

    /// <summary>GET /api/edi/history?partner=&docType=&dir=&max=200</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? partner = null,
        [FromQuery] string? docType = null,
        [FromQuery] string? dir     = null,
        [FromQuery] int     max     = 200)
    {
        var history = await _edi.GetHistory(partner, docType, dir, max);
        return Ok(history);
    }

    /// <summary>GET /api/edi/history/{id}</summary>
    [HttpGet("history/{id:int}")]
    public async Task<IActionResult> GetTransaction(int id)
    {
        var tx = await _edi.GetTransaction(id);
        return tx == null ? NotFound() : Ok(tx);
    }

    // ── Partners ───────────────────────────────────────────────────────────────

    /// <summary>GET /api/edi/partners</summary>
    [HttpGet("partners")]
    public async Task<IActionResult> GetPartners([FromQuery] bool activeOnly = true)
        => Ok(await _edi.GetPartners(activeOnly));

    /// <summary>GET /api/edi/partners/{id}</summary>
    [HttpGet("partners/{id}")]
    public async Task<IActionResult> GetPartner(string id)
    {
        var p = await _edi.GetPartner(id);
        return p == null ? NotFound() : Ok(p);
    }

    /// <summary>POST /api/edi/partners — create or update partner</summary>
    [HttpPost("partners")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SavePartner([FromBody] EdpPartner partner)
    {
        NullCoalesce(partner);
        var result = await _edi.SavePartner(partner);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Cross-references ───────────────────────────────────────────────────────

    /// <summary>GET /api/edi/xrefs?partner=ACME</summary>
    [HttpGet("xrefs")]
    public async Task<IActionResult> GetXrefs([FromQuery] string? partner = null)
        => Ok(await _edi.GetXrefs(partner));

    /// <summary>POST /api/edi/xrefs</summary>
    [HttpPost("xrefs")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SaveXref([FromBody] EdiXref xref)
    {
        xref.ExrBsgs    ??= string.Empty;
        xref.ExrTpaddr  ??= string.Empty;
        xref.ExrBsaddr  ??= string.Empty;
        xref.ExrType    ??= string.Empty;
        var result = await _edi.SaveXref(xref);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/edi/xrefs/{id}</summary>
    [HttpDelete("xrefs/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteXref(int id)
    {
        var result = await _edi.DeleteXref(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // ── Document definitions ───────────────────────────────────────────────────

    /// <summary>GET /api/edi/docs?partner=ACME</summary>
    [HttpGet("docs")]
    public async Task<IActionResult> GetDocDefs([FromQuery] string? partner = null)
        => Ok(await _edi.GetDocDefs(partner));

    /// <summary>POST /api/edi/docs</summary>
    [HttpPost("docs")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SaveDocDef([FromBody] EdiDoc doc)
    {
        doc.EddId      ??= string.Empty;
        doc.EddDesc    ??= string.Empty;
        doc.EddType    ??= string.Empty;
        doc.EddPartner ??= string.Empty;
        doc.EddMap     ??= string.Empty;
        doc.EddDir     ??= string.Empty;
        var result = await _edi.SaveDocDef(doc);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void NullCoalesce(EdpPartner p)
    {
        p.EdpId    ??= string.Empty;
        p.EdpDesc  ??= string.Empty;
        p.EdpSite  ??= string.Empty;
        p.EdpType  ??= string.Empty;
        p.EdpIsa   ??= string.Empty;
        p.EdpGs    ??= string.Empty;
        p.EdpFtpid ??= string.Empty;
        p.EdpAs2id ??= string.Empty;
        p.EdpNote  ??= string.Empty;
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record InboundEdiRequest(string Raw, string Partner, string? Site = null);
public record InboundFileRequest(string FilePath, string Partner, string? Site = null);
public record Ack997Request(string Raw, string Partner, string AckCode = "A", string? Note = null);
