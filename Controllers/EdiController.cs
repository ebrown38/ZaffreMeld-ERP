using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Services.EDI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZaffreMeld.Web.Controllers;

[Authorize(Roles = "admin,edi")]
[Route("edi")]
public class EdiController : Controller
{
    private readonly IEdiService _edi;
    private readonly ILogger<EdiController> _logger;

    public EdiController(IEdiService edi, ILogger<EdiController> logger)
    {
        _edi    = edi;
        _logger = logger;
    }

    // ── History / Dashboard ────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? partner = null,
        [FromQuery] string? docType = null,
        [FromQuery] string? dir     = null)
    {
        ViewBag.History  = await _edi.GetHistory(partner, docType, dir, 300);
        ViewBag.Partners = await _edi.GetPartners();
        ViewBag.Partner  = partner;
        ViewBag.DocType  = docType;
        ViewBag.Dir      = dir;
        return View();
    }

    [HttpGet("history/{id:int}")]
    public async Task<IActionResult> Transaction(int id)
    {
        var tx = await _edi.GetTransaction(id);
        if (tx == null) return NotFound();
        return View(tx);
    }

    // ── Inbound capture ────────────────────────────────────────────────────────

    [HttpGet("capture")]
    public async Task<IActionResult> Capture()
    {
        ViewBag.Partners = await _edi.GetPartners();
        return View();
    }

    [HttpPost("capture")]
    public async Task<IActionResult> Capture(
        [FromForm] string raw,
        [FromForm] string partner,
        [FromForm] string? site)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(partner))
        {
            TempData["Error"] = "Raw X12 and partner are required.";
            ViewBag.Partners  = await _edi.GetPartners();
            return View();
        }

        var result = await _edi.ProcessInbound(raw, partner, site ?? "DEFAULT");

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // ── Outbound generation ────────────────────────────────────────────────────

    [HttpGet("generate")]
    public async Task<IActionResult> Generate()
    {
        ViewBag.Partners = await _edi.GetPartners();
        return View();
    }

    [HttpPost("generate/810")]
    public async Task<IActionResult> Generate810(
        [FromForm] string arId, [FromForm] string partner)
    {
        var result = await _edi.Generate810(arId, partner);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (result.Success)
        {
            var data = result.Data as dynamic;
            TempData["GeneratedX12"] = data?.X12?.ToString();
        }
        return RedirectToAction(nameof(Generate));
    }

    [HttpPost("generate/856")]
    public async Task<IActionResult> Generate856(
        [FromForm] string shipId, [FromForm] string partner)
    {
        var result = await _edi.Generate856(shipId, partner);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (result.Success)
        {
            var data = result.Data as dynamic;
            TempData["GeneratedX12"] = data?.X12?.ToString();
        }
        return RedirectToAction(nameof(Generate));
    }

    // ── Partners ───────────────────────────────────────────────────────────────

    [HttpGet("partners")]
    public async Task<IActionResult> Partners()
    {
        ViewBag.Partners = await _edi.GetPartners(activeOnly: false);
        return View();
    }

    [HttpGet("partners/{id}")]
    public async Task<IActionResult> Partner(string id)
    {
        var partner = await _edi.GetPartner(id);
        if (partner == null) return NotFound();
        ViewBag.Xrefs   = await _edi.GetXrefs(id);
        ViewBag.DocDefs = await _edi.GetDocDefs(id);
        ViewBag.History = await _edi.GetHistory(id, max: 50);
        return View(partner);
    }

    [HttpGet("partners/new")]
    public IActionResult NewPartner() => View(new EdpPartner { EdpActive = true });

    [HttpPost("partners/new")]
    public async Task<IActionResult> NewPartner([FromForm] EdpPartner partner)
    {
        NullCoalesce(partner);
        var result = await _edi.SavePartner(partner);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return result.Success
            ? RedirectToAction(nameof(Partner), new { id = partner.EdpId })
            : View(partner);
    }

    [HttpPost("partners/{id}/edit")]
    public async Task<IActionResult> EditPartner(string id, [FromForm] EdpPartner partner)
    {
        partner.EdpId = id;
        NullCoalesce(partner);
        var result = await _edi.SavePartner(partner);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Partner), new { id });
    }

    // ── Cross-references ───────────────────────────────────────────────────────

    [HttpPost("xrefs/save")]
    public async Task<IActionResult> SaveXref([FromForm] EdiXref xref, [FromForm] string returnPartner)
    {
        xref.ExrBsgs   ??= string.Empty;
        xref.ExrTpaddr ??= string.Empty;
        xref.ExrBsaddr ??= string.Empty;
        xref.ExrType   ??= string.Empty;
        var result = await _edi.SaveXref(xref);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Partner), new { id = returnPartner });
    }

    [HttpPost("xrefs/{id:int}/delete")]
    public async Task<IActionResult> DeleteXref(int id, [FromForm] string returnPartner)
    {
        await _edi.DeleteXref(id);
        return RedirectToAction(nameof(Partner), new { id = returnPartner });
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
