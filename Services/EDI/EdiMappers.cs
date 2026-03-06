using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Shipping;

namespace ZaffreMeld.Web.Services.EDI;

// ── Mapping result types ───────────────────────────────────────────────────────

public record EdiMapResult(bool Success, string Message, object? Data = null)
{
    public static EdiMapResult Ok(object? data = null, string msg = "Mapped successfully.")
        => new(true, msg, data);
    public static EdiMapResult Error(string msg) => new(false, msg);
}

public record MappedPurchaseOrder(
    SoMstr Header,
    List<SodDet> Lines,
    string TradingPartner,
    string EdiControlNum);

public record MappedInvoice856(string RawX12);

// ── 850 Purchase Order (inbound) ───────────────────────────────────────────────

/// <summary>
/// Maps an inbound X12 850 (Purchase Order) to a ZaffreMeld SoMstr + SodDet list.
/// Mirrors the Java edi850.java mapping logic from the original ZaffreMeld project.
///
/// Segment flow:
///   ISA GS ST
///   BEG — beginning of PO (BEG03=PO#, BEG05=date)
///   REF — reference numbers
///   DTM — dates (DTM01=002 delivery, DTM01=001 ship)
///   N1*BT — bill-to party
///   N1*ST — ship-to party (N102=name, N3=addr, N4=city/state/zip)
///   PO1 loop:
///     PO1 — line (PO102=qty, PO104=price, PO107=buyer item, PO109=vendor item)
///     PID — product description (PID05=desc)
///     REF — line reference
///   CTT — transaction totals (CTT01=line count)
///   SE GE IEA
/// </summary>
public static class Map850Inbound
{
    public static EdiMapResult Map(X12Document doc, string partnerCode, EdiXref? xref)
    {
        try
        {
            var beg = X12Parser.Find(doc, "BEG");
            if (beg == null) return EdiMapResult.Error("Missing BEG segment.");

            var poNum    = beg[2];   // BEG03
            var poDate   = beg[4];   // BEG05 YYYYMMDD
            var purpose  = beg[0];   // BEG01 00=original 05=replace 06=cancel

            // Normalise date
            var entDate = poDate.Length == 8
                ? $"{poDate[..4]}-{poDate[4..6]}-{poDate[6..]}"
                : DateTime.Today.ToString("yyyy-MM-dd");

            // DTM — delivery/request date
            var reqDate = entDate;
            foreach (var dtm in X12Parser.FindAll(doc, "DTM"))
            {
                if (dtm[0] == "002" || dtm[0] == "010")
                {
                    var raw = dtm[1];
                    if (raw.Length == 8)
                        reqDate = $"{raw[..4]}-{raw[4..6]}-{raw[6..]}";
                }
            }

            // N1 — party identification
            var custCode = string.Empty;
            var shipName = string.Empty;
            var shipAddr = string.Empty;
            var shipCity = string.Empty;
            var shipState= string.Empty;
            var shipZip  = string.Empty;

            // Iterate segments to capture N1 loops
            string? currentN1 = null;
            foreach (var seg in doc.Segments)
            {
                switch (seg.Id)
                {
                    case "N1":
                        currentN1 = seg[0]; // BT, ST, BY, SE …
                        if (seg[0] == "BY" || seg[0] == "BT")
                        {
                            // Use xref to translate trading-partner GS id → customer code
                            custCode = xref?.ExrBsaddr ?? seg[1];
                        }
                        if (seg[0] == "ST") shipName = seg[1];
                        break;
                    case "N3":
                        if (currentN1 == "ST") shipAddr = seg[0];
                        break;
                    case "N4":
                        if (currentN1 == "ST")
                        {
                            shipCity  = seg[0];
                            shipState = seg[1];
                            shipZip   = seg[2];
                        }
                        break;
                }
            }

            // Build header
            var so = new SoMstr
            {
                SoNbr     = string.Empty, // assigned by counter in service
                SoCust    = custCode,
                SoEntdate = entDate,
                SoReqdate = reqDate,
                SoStatus  = "O",
                SoCurr    = "USD",
                SoNote    = $"EDI 850 from {partnerCode} — PO#{poNum}",
                SoUser    = "EDI"
            };

            // PO1 loops → SodDet lines
            var lines     = new List<SodDet>();
            var po1Loops  = X12Parser.FindLoops(doc, "PO1");
            var lineNum   = 0;

            foreach (var loop in po1Loops)
            {
                lineNum++;
                var po1  = loop.First(s => s.Id == "PO1");
                var pid  = loop.FirstOrDefault(s => s.Id == "PID");
                var lRef = loop.FirstOrDefault(s => s.Id == "REF");

                if (!decimal.TryParse(po1[1], out var qty))   qty   = 1;
                if (!decimal.TryParse(po1[3], out var price)) price = 0;

                // PO1 qualifier pairs: PO106/PO107 = buyer item, PO108/PO109 = vendor item
                // Elements are 0-based after segment ID is stripped, so qualifiers start at index 5
                var itemNum = string.Empty;
                for (var i = 5; i < po1.Elements.Length - 1; i += 2)
                {
                    var qual = po1[i];
                    var val  = po1[i + 1];
                    if (qual is "BP" or "VN" or "IN" && string.IsNullOrEmpty(itemNum))
                        itemNum = val;
                }

                var desc = pid != null ? pid[4] : string.Empty;

                lines.Add(new SodDet
                {
                    SodNbr    = string.Empty, // filled in by service
                    SodLine   = lineNum,
                    SodItem   = itemNum,
                    SodDesc   = desc,
                    SodQty    = qty,
                    SodPrice  = price,
                    SodUom    = po1[2],
                    SodStatus = "O"
                });
            }

            if (lines.Count == 0)
                return EdiMapResult.Error("No PO1 line segments found in 850.");

            var result = new MappedPurchaseOrder(so, lines, partnerCode, doc.IsaControlNum);
            return EdiMapResult.Ok(result, $"850 mapped: {lines.Count} lines, PO#{poNum}");
        }
        catch (Exception ex)
        {
            return EdiMapResult.Error($"850 mapping failed: {ex.Message}");
        }
    }
}

// ── 810 Invoice (outbound) ─────────────────────────────────────────────────────

/// <summary>
/// Builds an outbound X12 810 (Invoice) from a ZaffreMeld ArMstr + ArdMstr.
/// Mirrors the Java edi810.java generation logic.
///
/// Segment flow: BIG, REF, N1*SE, N1*BY, IT1 loop, TDS, SE
/// </summary>
public static class Map810Outbound
{
    public static string Build(
        ArMstr invoice,
        List<Models.Finance.ArdMstr> lines,
        EdpPartner partner,
        string ourIsaId, string ourGsId)
    {
        var invDate = DateTime.Today.ToString("yyyyMMdd");
        var total   = invoice.ArAmt.ToString("F2").Replace(".", "");

        return X12Builder.BuildEnvelope(
            ourIsaId, partner.EdpIsa,
            ourGsId,  partner.EdpGs,
            "IN", "810", invoice.ArId,
            b =>
            {
                // BIG — beginning segment for invoice
                b.Seg("BIG", invDate, invoice.ArId, invoice.ArEntdate, invoice.ArRef);

                // REF — customer account
                b.Seg("REF", "VR", invoice.ArCust);

                // N1*SE — seller
                b.Seg("N1", "SE", ourGsId, "92", ourIsaId);

                // N1*BY — buyer
                b.Seg("N1", "BY", invoice.ArCust, "92", partner.EdpId);

                // IT1 line loops
                var lineNum = 0;
                foreach (var line in lines)
                {
                    lineNum++;
                    var qty   = "1";
                    var price = line.ArdAmt.ToString("F2");
                    b.Seg("IT1",
                        lineNum.ToString(),
                        qty, "EA",
                        price,
                        "PE",          // price qualifier
                        "BP", line.ArdItem,
                        "PO", line.ArdRef);

                    if (!string.IsNullOrEmpty(line.ArdItem))
                        b.Seg("PID", "F", "", "", "", line.ArdItem);
                }

                // TDS — total dollar amount (in cents, no decimal)
                b.Seg("TDS", total);

                // CTT — transaction totals
                b.Seg("CTT", lineNum.ToString());

                return b;
            });
    }
}

// ── 856 ASN / Advance Ship Notice (outbound) ───────────────────────────────────

/// <summary>
/// Builds an outbound X12 856 (Ship Notice/Manifest) from a ZaffreMeld ShipMstr.
/// Mirrors the Java edi856.java generation logic.
///
/// Segment flow: BSN, DTM, HL*S shipment loop, HL*O order loop, HL*I item loop, CTT
/// </summary>
public static class Map856Outbound
{
    public static string Build(
        ShipMstr shipment,
        List<ShipDet> lines,
        EdpPartner partner,
        string ourIsaId, string ourGsId)
    {
        var shipDate = string.IsNullOrEmpty(shipment.ShShipdate)
            ? DateTime.Today.ToString("yyyyMMdd")
            : shipment.ShShipdate.Replace("-", "");

        var hl = 0; // HL counter

        return X12Builder.BuildEnvelope(
            ourIsaId, partner.EdpIsa,
            ourGsId,  partner.EdpGs,
            "SH", "856", shipment.ShId,
            b =>
            {
                // BSN — beginning segment for ship notice
                b.Seg("BSN", "00", shipment.ShId,
                    shipDate, DateTime.Now.ToString("HHmm"), "0002");

                // DTM — ship date
                b.Seg("DTM", "011", shipDate);

                // HL*S — shipment level
                hl++;
                b.Seg("HL", hl.ToString(), "", "S", "1");
                b.Seg("TD1", "CTN", "1", "", "", "", "", shipment.ShWeight.ToString("F2"), "LB");
                b.Seg("TD5", "B", "2", shipment.ShCarrier);
                if (!string.IsNullOrEmpty(shipment.ShTrackno))
                    b.Seg("REF", "CN", shipment.ShTrackno);

                // Group lines by SO number
                var byOrder = lines.GroupBy(l => l.ShdSo);
                foreach (var orderGroup in byOrder)
                {
                    var parentHl = hl;
                    // HL*O — order level
                    hl++;
                    b.Seg("HL", hl.ToString(), parentHl.ToString(), "O", "1");
                    b.Seg("PRF", orderGroup.Key);

                    foreach (var line in orderGroup)
                    {
                        var itemHl = hl;
                        // HL*I — item level
                        hl++;
                        b.Seg("HL", hl.ToString(), itemHl.ToString(), "I", "0");
                        b.Seg("LIN", line.ShdLine.ToString(), "BP", line.ShdItem);
                        b.Seg("SN1", line.ShdLine.ToString(),
                            line.ShdQty.ToString("F0"), line.ShdUom);

                        if (!string.IsNullOrEmpty(line.ShdLot))
                            b.Seg("LOT", line.ShdLot);
                    }
                }

                // CTT — transaction totals
                b.Seg("CTT", lines.Count.ToString());

                return b;
            });
    }
}

// ── 997 Functional Acknowledgment (outbound) ───────────────────────────────────

/// <summary>
/// Builds an outbound X12 997 (Functional Acknowledgment).
/// Sent in response to every inbound document.
/// Mirrors the Java edi997.java generation logic.
/// </summary>
public static class Map997Outbound
{
    public static string Build(
        X12Document inboundDoc,
        string ackCode,        // A=Accepted, R=Rejected, E=Accepted with errors
        EdpPartner partner,
        string ourIsaId, string ourGsId,
        string? errorNote = null)
    {
        return X12Builder.BuildEnvelope(
            ourIsaId, partner.EdpIsa,
            ourGsId,  partner.EdpGs,
            "FA", "997", inboundDoc.IsaControlNum,
            b =>
            {
                // AK1 — functional group response
                b.Seg("AK1", inboundDoc.GsFuncId, inboundDoc.GsControlNum);

                // AK2 — transaction set response
                b.Seg("AK2", inboundDoc.StDocType, inboundDoc.StControlNum);

                // AK5 — transaction set acknowledgment
                b.Seg("AK5", ackCode);

                if (ackCode != "A" && errorNote != null)
                    b.Seg("IK5", errorNote[..Math.Min(errorNote.Length, 35)]);

                // AK9 — functional group acknowledgment
                b.Seg("AK9", ackCode, "1", "1", ackCode == "A" ? "1" : "0");

                return b;
            });
    }
}