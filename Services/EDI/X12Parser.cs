using System.Text;

namespace ZaffreMeld.Web.Services.EDI;

/// <summary>
/// Parses and builds ANSI X12 EDI documents.
/// Handles ISA/GS/ST envelope wrapping and all common segment types.
/// Mirrors the Java ediTran.java parsing logic from the original ZaffreMeld.
/// </summary>
public class X12Document
{
    public string IsaSenderId   { get; set; } = string.Empty;
    public string IsaReceiverId { get; set; } = string.Empty;
    public string IsaDate       { get; set; } = string.Empty;
    public string IsaTime       { get; set; } = string.Empty;
    public string IsaControlNum { get; set; } = string.Empty;
    public string GsFuncId      { get; set; } = string.Empty;   // e.g. PO, IN, SH, FA
    public string GsSenderId    { get; set; } = string.Empty;
    public string GsReceiverId  { get; set; } = string.Empty;
    public string GsControlNum  { get; set; } = string.Empty;
    public string StDocType     { get; set; } = string.Empty;   // 850, 810, 856, 997 …
    public string StControlNum  { get; set; } = string.Empty;

    public List<X12Segment> Segments { get; set; } = new();

    public char ElementSep  { get; set; } = '*';
    public char SegmentTerm { get; set; } = '~';
    public char CompSep     { get; set; } = ':';
}

public record X12Segment(string Id, string[] Elements)
{
    public string this[int i] => i < Elements.Length ? Elements[i] : string.Empty;
}

public static class X12Parser
{
    /// <summary>Parse a raw X12 string into an X12Document.</summary>
    public static X12Document Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Empty EDI input.");

        // ISA is always 106 chars with fixed positions for delimiters
        if (raw.Length < 106)
            throw new ArgumentException("Input too short to contain ISA segment.");

        var doc = new X12Document
        {
            ElementSep  = raw[3],
            CompSep     = raw[104],
            SegmentTerm = raw[105]
        };

        // Split on segment terminator, strip whitespace/newlines
        var rawSegs = raw.Split(doc.SegmentTerm)
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0)
                         .ToList();

        foreach (var seg in rawSegs)
        {
            var parts = seg.Split(doc.ElementSep);
            var segId = parts[0].Trim();
            var elems = parts.Skip(1).ToArray();
            var x12   = new X12Segment(segId, elems);

            switch (segId)
            {
                case "ISA":
                    doc.IsaSenderId   = x12[5].Trim();
                    doc.IsaReceiverId = x12[7].Trim();
                    doc.IsaDate       = x12[8];
                    doc.IsaTime       = x12[9];
                    doc.IsaControlNum = x12[12];
                    break;
                case "GS":
                    doc.GsFuncId     = x12[0];
                    doc.GsSenderId   = x12[1];
                    doc.GsReceiverId = x12[2];
                    doc.GsControlNum = x12[5];
                    break;
                case "ST":
                    doc.StDocType    = x12[0];
                    doc.StControlNum = x12[1];
                    break;
            }

            doc.Segments.Add(x12);
        }

        return doc;
    }

    /// <summary>Find all segments with the given ID.</summary>
    public static IEnumerable<X12Segment> FindAll(X12Document doc, string segId)
        => doc.Segments.Where(s => s.Id == segId);

    /// <summary>Find first segment with given ID, or null.</summary>
    public static X12Segment? Find(X12Document doc, string segId)
        => doc.Segments.FirstOrDefault(s => s.Id == segId);

    /// <summary>
    /// Find all segments between two anchor segment IDs (exclusive).
    /// Used to extract line-level loops, e.g. PO1 loops in an 850.
    /// </summary>
    public static List<List<X12Segment>> FindLoops(X12Document doc, string loopStart)
    {
        var loops = new List<List<X12Segment>>();
        List<X12Segment>? current = null;

        foreach (var seg in doc.Segments)
        {
            if (seg.Id == loopStart)
            {
                current = new List<X12Segment>();
                loops.Add(current);
            }
            current?.Add(seg);
        }

        return loops;
    }
}

/// <summary>Builds outbound X12 EDI documents.</summary>
public class X12Builder
{
    private readonly StringBuilder _sb = new();
    private readonly char _el;
    private readonly char _st;
    private int _segCount;

    public X12Builder(char elementSep = '*', char segTerm = '~')
    {
        _el = elementSep;
        _st = segTerm;
    }

    public X12Builder Seg(string id, params string[] elements)
    {
        _sb.Append(id);
        foreach (var e in elements)
        {
            _sb.Append(_el);
            _sb.Append(e);
        }
        _sb.Append(_st);
        _sb.Append('\n');
        _segCount++;
        return this;
    }

    /// <summary>Wrap content in ISA/GS/ST/SE/GE/IEA envelope.</summary>
    public static string BuildEnvelope(
        string sendIsa, string rcvIsa,
        string sendGs,  string rcvGs,
        string funcId,  string docType,
        string ctrlNum,
        Func<X12Builder, X12Builder> bodyBuilder)
    {
        var now = DateTime.Now;
        var date = now.ToString("yyMMdd");
        var time = now.ToString("HHmm");
        var ctrl6 = ctrlNum.PadLeft(9, '0');

        var body = new X12Builder();
        bodyBuilder(body);
        var bodyStr  = body._sb.ToString();
        var segCount = body._segCount + 2; // +2 for ST and SE

        var outer = new X12Builder();
        outer.Seg("ISA",
            "00", "          ",
            "00", "          ",
            "ZZ", sendIsa.PadRight(15),
            "ZZ", rcvIsa.PadRight(15),
            date, time, "^", "00501",
            ctrl6, "0", "P", ":");

        outer.Seg("GS", funcId, sendGs, rcvGs,
            now.ToString("yyyyMMdd"), time,
            ctrlNum, "X", "005010");

        outer.Seg("ST", docType, "0001");

        outer._sb.Append(bodyStr);
        outer._segCount += body._segCount;

        outer.Seg("SE", segCount.ToString(), "0001");
        outer.Seg("GE", "1", ctrlNum);
        outer.Seg("IEA", "1", ctrl6);

        return outer._sb.ToString();
    }
}
