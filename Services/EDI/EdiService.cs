using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Models.Shipping;
using ZaffreMeld.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ZaffreMeld.Web.Services.EDI;

// ── Interface ──────────────────────────────────────────────────────────────────

public interface IEdiService
{
    // Inbound
    Task<EdiMapResult> ProcessInbound(string rawX12, string partnerCode, string site);
    Task<EdiMapResult> ProcessInboundFile(string filePath, string partnerCode, string site);

    // Outbound
    Task<EdiMapResult> Generate810(string arId, string partnerCode);
    Task<EdiMapResult> Generate856(string shipId, string partnerCode);
    Task<string>       Generate997(string rawInbound, string ackCode, string partnerCode, string? note = null);

    // Browse
    Task<List<EdiMstr>>    GetHistory(string? partner = null, string? docType = null,
                                      string? dir = null, int max = 200);
    Task<EdiMstr?>         GetTransaction(int id);
    Task<List<EdpPartner>> GetPartners(bool activeOnly = true);
    Task<EdpPartner?>      GetPartner(string id);
    Task<EdiMapResult>     SavePartner(EdpPartner partner);
    Task<List<EdiXref>>    GetXrefs(string? partner = null);
    Task<EdiMapResult>     SaveXref(EdiXref xref);
    Task<EdiMapResult>     DeleteXref(int id);
    Task<List<EdiDoc>>     GetDocDefs(string? partner = null);
    Task<EdiMapResult>     SaveDocDef(EdiDoc doc);
}

// ── Implementation ─────────────────────────────────────────────────────────────

public class EdiService : IEdiService
{
    private readonly ZaffreMeldDbContext _db;
    private readonly IOrderService     _orders;
    private readonly IConfiguration    _config;
    private readonly ILogger<EdiService> _logger;

    // Our ISA/GS identifiers from config
    private string OurIsaId => _config["Edi:OurIsaId"] ?? "ZAFFREMELD       ";
    private string OurGsId  => _config["Edi:OurGsId"]  ?? "ZAFFREMELD";

    public EdiService(
        ZaffreMeldDbContext db,
        IOrderService orders,
        IConfiguration config,
        ILogger<EdiService> logger)
    {
        _db     = db;
        _orders = orders;
        _config = config;
        _logger = logger;
    }

    // ── Inbound ────────────────────────────────────────────────────────────────

    public async Task<EdiMapResult> ProcessInbound(string rawX12, string partnerCode, string site)
    {
        X12Document doc;
        try
        {
            doc = X12Parser.Parse(rawX12);
        }
        catch (Exception ex)
        {
            await LogTransaction(partnerCode, "???", "IN", "E", site, rawX12[..Math.Min(rawX12.Length, 500)]);
            return EdiMapResult.Error($"X12 parse error: {ex.Message}");
        }

        var docType = doc.StDocType;
        _logger.LogInformation("EDI inbound {DocType} from {Partner} ctrl={Ctrl}",
            docType, partnerCode, doc.IsaControlNum);

        // Look up xref for this trading partner
        var xref = await _db.EdiXref
            .FirstOrDefaultAsync(x => x.ExrTpaddr == doc.IsaSenderId.Trim() && x.ExrActive);

        EdiMapResult result = docType switch
        {
            "850" => await ProcessInbound850(doc, partnerCode, xref, site),
            "997" => ProcessInbound997(doc, partnerCode),
            _     => EdiMapResult.Error($"Unsupported inbound doc type: {docType}")
        };

        var status = result.Success ? "A" : "E";
        await LogTransaction(partnerCode, docType, "IN", status, site, rawX12);

        return result;
    }

    public async Task<EdiMapResult> ProcessInboundFile(string filePath, string partnerCode, string site)
    {
        if (!File.Exists(filePath))
            return EdiMapResult.Error($"File not found: {filePath}");

        var raw = await File.ReadAllTextAsync(filePath);
        return await ProcessInbound(raw, partnerCode, site);
    }

    private async Task<EdiMapResult> ProcessInbound850(
        X12Document doc, string partnerCode, EdiXref? xref, string site)
    {
        var mapped = Map850Inbound.Map(doc, partnerCode, xref);
        if (!mapped.Success) return mapped;

        var order = (MappedPurchaseOrder)mapped.Data!;
        var so    = order.Header;
        so.SoSite = site;

        // Assign order number
        so.SoNbr = await GetNextNumber("SO");

        // Fill line numbers
        foreach (var line in order.Lines)
            line.SodNbr = so.SoNbr;

        // Create the sales order
        var createResult = await _orders.CreateSalesOrder(so, order.Lines);
        if (!createResult.Success)
            return EdiMapResult.Error($"850 SO create failed: {createResult.Message}");

        // Auto-generate 997 acknowledgment
        var partner = await GetPartner(partnerCode);
        if (partner != null)
        {
            var fa = Map997Outbound.Build(doc, "A", partner, OurIsaId, OurGsId);
            await LogTransaction(partnerCode, "997", "OUT", "A", site, fa);
        }

        return EdiMapResult.Ok(new { SoNbr = so.SoNbr },
            $"850 processed: SO {so.SoNbr} created with {order.Lines.Count} lines.");
    }

    private static EdiMapResult ProcessInbound997(X12Document doc, string partnerCode)
    {
        var ak9 = X12Parser.Find(doc, "AK9");
        var ackCode = ak9?[0] ?? "?";
        return EdiMapResult.Ok(null,
            $"997 FA received from {partnerCode}: {(ackCode == "A" ? "Accepted" : $"Code={ackCode}")}");
    }

    // ── Outbound ───────────────────────────────────────────────────────────────

    public async Task<EdiMapResult> Generate810(string arId, string partnerCode)
    {
        var invoice = await _db.ArMstr.FindAsync(arId);
        if (invoice == null) return EdiMapResult.Error($"Invoice {arId} not found.");

        var lines = await _db.ArdMstr
            .Where(l => l.ArdId == arId)
            .OrderBy(l => l.ArdLine)
            .ToListAsync();

        var partner = await GetPartner(partnerCode);
        if (partner == null) return EdiMapResult.Error($"Partner {partnerCode} not found.");

        var x12 = Map810Outbound.Build(invoice, lines, partner, OurIsaId, OurGsId);
        await LogTransaction(partnerCode, "810", "OUT", "A",
            invoice.ArShip ?? string.Empty, x12);

        return EdiMapResult.Ok(new { X12 = x12 }, $"810 generated for invoice {arId}.");
    }

    public async Task<EdiMapResult> Generate856(string shipId, string partnerCode)
    {
        var ship = await _db.ShipMstr.FindAsync(shipId);
        if (ship == null) return EdiMapResult.Error($"Shipper {shipId} not found.");

        var lines = await _db.ShipDet
            .Where(l => l.ShdId == shipId)
            .OrderBy(l => l.ShdLine)
            .ToListAsync();

        var partner = await GetPartner(partnerCode);
        if (partner == null) return EdiMapResult.Error($"Partner {partnerCode} not found.");

        var x12 = Map856Outbound.Build(ship, lines, partner, OurIsaId, OurGsId);
        await LogTransaction(partnerCode, "856", "OUT", "A", ship.ShSite, x12);

        return EdiMapResult.Ok(new { X12 = x12 }, $"856 generated for shipper {shipId}.");
    }

    public async Task<string> Generate997(
        string rawInbound, string ackCode, string partnerCode, string? note = null)
    {
        var doc     = X12Parser.Parse(rawInbound);
        var partner = await GetPartner(partnerCode) ?? new EdpPartner
        {
            EdpIsa = doc.IsaSenderId.Trim(),
            EdpGs  = doc.GsSenderId.Trim()
        };

        return Map997Outbound.Build(doc, ackCode, partner, OurIsaId, OurGsId, note);
    }

    // ── Browse / Config ────────────────────────────────────────────────────────

    public async Task<List<EdiMstr>> GetHistory(
        string? partner = null, string? docType = null,
        string? dir = null, int max = 200)
    {
        var q = _db.EdiMstr.AsQueryable();
        if (partner != null) q = q.Where(e => e.EdiPartner == partner);
        if (docType != null) q = q.Where(e => e.EdiDoc2    == docType);
        if (dir     != null) q = q.Where(e => e.EdiDir     == dir);
        return await q.OrderByDescending(e => e.EdiTimestamp).Take(max).ToListAsync();
    }

    public async Task<EdiMstr?> GetTransaction(int id)
        => await _db.EdiMstr.FindAsync(id);

    public async Task<List<EdpPartner>> GetPartners(bool activeOnly = true)
    {
        var q = _db.EdpPartner.AsQueryable();
        if (activeOnly) q = q.Where(p => p.EdpActive);
        return await q.OrderBy(p => p.EdpId).ToListAsync();
    }

    public async Task<EdpPartner?> GetPartner(string id)
        => await _db.EdpPartner.FindAsync(id);

    public async Task<EdiMapResult> SavePartner(EdpPartner partner)
    {
        try
        {
            var existing = await _db.EdpPartner.FindAsync(partner.EdpId);
            if (existing == null) _db.EdpPartner.Add(partner);
            else                  _db.EdpPartner.Update(partner);
            await _db.SaveChangesAsync();
            return EdiMapResult.Ok(null, "Partner saved.");
        }
        catch (Exception ex)
        {
            return EdiMapResult.Error(ex.Message);
        }
    }

    public async Task<List<EdiXref>> GetXrefs(string? partner = null)
    {
        var q = _db.EdiXref.AsQueryable();
        if (partner != null) q = q.Where(x => x.ExrTpaddr == partner);
        return await q.OrderBy(x => x.ExrType).ToListAsync();
    }

    public async Task<EdiMapResult> SaveXref(EdiXref xref)
    {
        try
        {
            if (xref.Id == 0) _db.EdiXref.Add(xref);
            else               _db.EdiXref.Update(xref);
            await _db.SaveChangesAsync();
            return EdiMapResult.Ok(null, "Cross-reference saved.");
        }
        catch (Exception ex)
        {
            return EdiMapResult.Error(ex.Message);
        }
    }

    public async Task<EdiMapResult> DeleteXref(int id)
    {
        var xref = await _db.EdiXref.FindAsync(id);
        if (xref == null) return EdiMapResult.Error("Xref not found.");
        _db.EdiXref.Remove(xref);
        await _db.SaveChangesAsync();
        return EdiMapResult.Ok(null, "Cross-reference deleted.");
    }

    public async Task<List<EdiDoc>> GetDocDefs(string? partner = null)
    {
        var q = _db.EdiDoc.AsQueryable();
        if (partner != null) q = q.Where(d => d.EddPartner == partner);
        return await q.OrderBy(d => d.EddPartner).ThenBy(d => d.EddType).ToListAsync();
    }

    public async Task<EdiMapResult> SaveDocDef(EdiDoc doc)
    {
        try
        {
            var existing = await _db.EdiDoc.FindAsync(doc.EddId);
            if (existing == null) _db.EdiDoc.Add(doc);
            else                  _db.EdiDoc.Update(doc);
            await _db.SaveChangesAsync();
            return EdiMapResult.Ok(null, "Document definition saved.");
        }
        catch (Exception ex)
        {
            return EdiMapResult.Error(ex.Message);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task LogTransaction(
        string partner, string docType, string dir,
        string status, string site, string rawData)
    {
        try
        {
            _db.EdiMstr.Add(new EdiMstr
            {
                EdiId        = $"{docType}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                EdiDoc2      = docType,
                EdiPartner   = partner,
                EdiDir       = dir,
                EdiStatus    = status,
                EdiSite      = site,
                EdiTimestamp = DateTime.UtcNow,
                EdiSndisa    = dir == "IN" ? OurIsaId : string.Empty,
                EdiRcvisa    = dir == "OUT" ? OurIsaId : string.Empty
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log EDI transaction");
        }
    }

    private async Task<string> GetNextNumber(string counterName)
    {
        var counter = await _db.Counters
            .FirstOrDefaultAsync(c => c.CounterName == counterName);
        if (counter == null) return $"{counterName}-{DateTime.Now:yyyyMMddHHmmss}";
        var num = counter.CounterValue++;
        await _db.SaveChangesAsync();
        return $"{counter.CounterPrefix}{num.ToString().PadLeft(counter.CounterLength, '0')}";
    }
}
