using FluentAssertions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Services.EDI;

namespace ZaffreMeld.Tests.Unit;

/// <summary>
/// Tests for X12Parser (inbound parsing) and X12Builder (outbound generation).
/// All tests use the standard 850 fixture from SampleEdi.
/// </summary>
public class X12ParserTests
{
    // ── Parse — guard conditions ───────────────────────────────────────────────

    [Fact]
    public void Parse_NullInput_Throws()
    {
        var act = () => X12Parser.Parse(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var act = () => X12Parser.Parse(string.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*Empty*");
    }

    [Fact]
    public void Parse_TooShort_Throws()
    {
        var act = () => X12Parser.Parse("ISA*short");
        act.Should().Throw<ArgumentException>().WithMessage("*too short*");
    }

    // ── Parse — ISA envelope ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidEdi850_ExtractsSenderFromIsa()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.IsaSenderId.Should().Be("ACMEPARTNER");
    }

    [Fact]
    public void Parse_ValidEdi850_ExtractsReceiverFromIsa()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.IsaReceiverId.Should().Be("ZAFFREMELD");
    }

    [Fact]
    public void Parse_ValidEdi850_ExtractsControlNumber()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.IsaControlNum.Should().Be("000000001");
    }

    [Fact]
    public void Parse_ValidEdi850_DetectsElementSeparator()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.ElementSep.Should().Be('*');
    }

    [Fact]
    public void Parse_ValidEdi850_DetectsSegmentTerminator()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.SegmentTerm.Should().Be('~');
    }

    // ── Parse — GS / ST ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidEdi850_ExtractsFuncId()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.GsFuncId.Should().Be("PO");
    }

    [Fact]
    public void Parse_ValidEdi850_ExtractsDocType()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.StDocType.Should().Be("850");
    }

    [Fact]
    public void Parse_Edi997_ExtractsDocType()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi997);
        doc.StDocType.Should().Be("997");
    }

    // ── Parse — segment count ─────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidEdi850_ParsesAllSegments()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        // ISA GS ST BEG REF DTM N1 N1 N3 N4 PO1 PID PO1 PID CTT SE GE IEA = 18
        doc.Segments.Should().HaveCount(18);
    }

    // ── Find ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Find_ExistingSegment_ReturnsIt()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        var beg = X12Parser.Find(doc, "BEG");

        beg.Should().NotBeNull();
        beg![2].Should().Be("PO-98765");   // BEG03 = PO number
    }

    [Fact]
    public void Find_NonExistentSegment_ReturnsNull()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        X12Parser.Find(doc, "FOO").Should().BeNull();
    }

    [Fact]
    public void Find_BEG_ReturnsPoDate()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        var beg = X12Parser.Find(doc, "BEG");
        beg![4].Should().Be("20260301");   // BEG05 = date
    }

    // ── FindAll ────────────────────────────────────────────────────────────────

    [Fact]
    public void FindAll_N1_ReturnsTwoParties()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        X12Parser.FindAll(doc, "N1").Should().HaveCount(2);
    }

    [Fact]
    public void FindAll_PO1_ReturnsTwoLines()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        X12Parser.FindAll(doc, "PO1").Should().HaveCount(2);
    }

    [Fact]
    public void FindAll_MissingSegment_ReturnsEmpty()
    {
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        X12Parser.FindAll(doc, "ZZZ").Should().BeEmpty();
    }

    // ── FindLoops ─────────────────────────────────────────────────────────────

    [Fact]
    public void FindLoops_PO1_ReturnsTwoLoops()
    {
        var doc   = X12Parser.Parse(SampleEdi.Edi850);
        var loops = X12Parser.FindLoops(doc, "PO1");

        loops.Should().HaveCount(2);
    }

    [Fact]
    public void FindLoops_EachLoopStartsWithPO1()
    {
        var doc   = X12Parser.Parse(SampleEdi.Edi850);
        var loops = X12Parser.FindLoops(doc, "PO1");

        loops.Should().AllSatisfy(loop => loop.First().Id.Should().Be("PO1"));
    }

    [Fact]
    public void FindLoops_FirstLoop_ContainsPid()
    {
        var doc   = X12Parser.Parse(SampleEdi.Edi850);
        var loops = X12Parser.FindLoops(doc, "PO1");

        loops[0].Should().Contain(s => s.Id == "PID");
    }

    [Fact]
    public void FindLoops_EmptyDoc_ReturnsEmpty()
    {
        var doc = new X12Document();
        X12Parser.FindLoops(doc, "PO1").Should().BeEmpty();
    }

    // ── X12Segment indexer ────────────────────────────────────────────────────

    [Fact]
    public void Segment_Indexer_ReturnsElementAtIndex()
    {
        var seg = new X12Segment("BEG", new[] { "00", "SA", "PO-001", "", "20260301" });
        seg[2].Should().Be("PO-001");
    }

    [Fact]
    public void Segment_Indexer_OutOfBounds_ReturnsEmpty()
    {
        var seg = new X12Segment("BEG", new[] { "00" });
        seg[99].Should().BeEmpty();
    }
}

public class X12BuilderTests
{
    // ── BuildEnvelope ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildEnvelope_ProducesIsaSegment()
    {
        var x12 = BuildSample997();
        x12.Should().Contain("ISA*");
    }

    [Fact]
    public void BuildEnvelope_ProducesIeaSegment()
    {
        var x12 = BuildSample997();
        x12.Should().Contain("IEA*");
    }

    [Fact]
    public void BuildEnvelope_ProducesGsSegment()
    {
        var x12 = BuildSample997();
        x12.Should().Contain("GS*");
    }

    [Fact]
    public void BuildEnvelope_ProducesStSegment()
    {
        var x12 = BuildSample997();
        x12.Should().Contain("ST*997*");
    }

    [Fact]
    public void BuildEnvelope_ProducesSeSegment()
    {
        var x12 = BuildSample997();
        x12.Should().Contain("SE*");
    }

    [Fact]
    public void BuildEnvelope_IsParseable_AfterBuild()
    {
        var x12  = BuildSample997();
        var act  = () => X12Parser.Parse(x12);
        act.Should().NotThrow();
    }

    [Fact]
    public void BuildEnvelope_ParsedDoc_HasCorrectDocType()
    {
        var x12 = BuildSample997();
        var doc = X12Parser.Parse(x12);
        doc.StDocType.Should().Be("997");
    }

    [Fact]
    public void BuildEnvelope_ParsedDoc_HasCorrectFuncId()
    {
        var x12 = BuildSample997();
        var doc = X12Parser.Parse(x12);
        doc.GsFuncId.Should().Be("FA");
    }

    [Fact]
    public void Seg_Method_AppendsCorrectly()
    {
        var builder = new X12Builder();
        builder.Seg("REF", "VR", "ACME");

        // Can't inspect private sb directly, so round-trip via parse trick:
        // just verify no exception
        var x12 = X12Builder.BuildEnvelope(
            "SENDER", "RECEIVER", "SENDER", "RECEIVER",
            "FA", "997", "1",
            b => { b.Seg("AK1", "PO", "1"); return b; });

        x12.Should().Contain("AK1*PO*1");
    }

    // ── Map997Outbound ────────────────────────────────────────────────────────

    [Fact]
    public void Map997_Accepted_ContainsAk5A()
    {
        var inbound = X12Parser.Parse(SampleEdi.Edi850);
        var partner = new Web.Models.EDI.EdpPartner { EdpId = "ACME", EdpIsa = "ACMEPARTNER", EdpGs = "ACMEPARTNER", EdpActive = true };

        var fa = Map997Outbound.Build(inbound, "A", partner, "ZAFFREMELD     ", "ZAFFREMELD");

        fa.Should().Contain("AK5*A");
    }

    [Fact]
    public void Map997_Rejected_ContainsAk5R()
    {
        var inbound = X12Parser.Parse(SampleEdi.Edi850);
        var partner = new Web.Models.EDI.EdpPartner { EdpId = "ACME", EdpIsa = "ACMEPARTNER", EdpGs = "ACMEPARTNER", EdpActive = true };

        var fa = Map997Outbound.Build(inbound, "R", partner, "ZAFFREMELD     ", "ZAFFREMELD", "Validation error");

        fa.Should().Contain("AK5*R");
        fa.Should().Contain("IK5*");
    }

    [Fact]
    public void Map997_ContainsAk9()
    {
        var inbound = X12Parser.Parse(SampleEdi.Edi850);
        var partner = new Web.Models.EDI.EdpPartner { EdpId = "ACME", EdpIsa = "ACMEPARTNER", EdpGs = "ACMEPARTNER", EdpActive = true };

        var fa = Map997Outbound.Build(inbound, "A", partner, "ZAFFREMELD     ", "ZAFFREMELD");

        fa.Should().Contain("AK9*A");
    }

    // ── Map850Inbound ─────────────────────────────────────────────────────────

    [Fact]
    public void Map850_ValidDoc_ReturnsSuccess()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Map850_ExtractsTwoLines()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order = (MappedPurchaseOrder)result.Data!;
        order.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void Map850_FirstLine_HasCorrectQtyAndPrice()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order = (MappedPurchaseOrder)result.Data!;
        var line1 = order.Lines[0];

        line1.SodQty.Should().Be(10m);
        line1.SodPrice.Should().Be(25.00m);
    }

    [Fact]
    public void Map850_SecondLine_HasCorrectItem()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order  = (MappedPurchaseOrder)result.Data!;
        var line2  = order.Lines[1];
        line2.SodItem.Should().Be("GADGET-200");
    }

    [Fact]
    public void Map850_WithXref_MapsCustomerCode()
    {
        var doc  = X12Parser.Parse(SampleEdi.Edi850);
        var xref = new Web.Models.EDI.EdiXref
        {
            ExrTpaddr = "ACMEPARTNER",
            ExrBsaddr = "ACME-CUST",
            ExrType   = "customer",
            ExrActive = true
        };

        var result = Map850Inbound.Map(doc, "ACME", xref);
        var order  = (MappedPurchaseOrder)result.Data!;

        order.Header.SoCust.Should().Be("ACME-CUST");
    }

    [Fact]
    public void Map850_NormalisesPoDate_To_IsoFormat()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order = (MappedPurchaseOrder)result.Data!;
        order.Header.SoEntdate.Should().Be("2026-03-01");
    }

    [Fact]
    public void Map850_ExtractsDtmDeliveryDate()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order = (MappedPurchaseOrder)result.Data!;
        order.Header.SoReqdate.Should().Be("2026-03-15");
    }

    [Fact]
    public void Map850_SetsStatusToOpen()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order = (MappedPurchaseOrder)result.Data!;
        order.Header.SoStatus.Should().Be("O");
    }

    [Fact]
    public void Map850_SetsUserToEdi()
    {
        var doc    = X12Parser.Parse(SampleEdi.Edi850);
        var result = Map850Inbound.Map(doc, "ACME", null);

        var order = (MappedPurchaseOrder)result.Data!;
        order.Header.SoUser.Should().Be("EDI");
    }

    [Fact]
    public void Map850_MissingBeg_ReturnsError()
    {
        // Build a doc with no BEG segment
        var doc = new X12Document();
        doc.Segments.Add(new X12Segment("ST", new[] { "850", "0001" }));

        var result = Map850Inbound.Map(doc, "ACME", null);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("BEG");
    }

    [Fact]
    public void Map850_NoPo1Lines_ReturnsError()
    {
        // Strip all PO1 segments
        var doc = X12Parser.Parse(SampleEdi.Edi850);
        doc.Segments.RemoveAll(s => s.Id is "PO1" or "PID");

        var result = Map850Inbound.Map(doc, "ACME", null);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No PO1");
    }

    // ── Map810Outbound ────────────────────────────────────────────────────────

    [Fact]
    public void Map810_ContainsBigSegment()
    {
        var x12 = Build810();
        x12.Should().Contain("BIG*");
    }

    [Fact]
    public void Map810_ContainsInvoiceId()
    {
        var x12 = Build810();
        x12.Should().Contain("INV-001");
    }

    [Fact]
    public void Map810_ContainsIt1ForEachLine()
    {
        var x12 = Build810();
        var it1Count = x12.Split('\n').Count(l => l.StartsWith("IT1*"));
        it1Count.Should().Be(2);
    }

    [Fact]
    public void Map810_ContainsTdsSegment()
    {
        var x12 = Build810();
        x12.Should().Contain("TDS*");
    }

    [Fact]
    public void Map810_IsParseable()
    {
        var x12 = Build810();
        var act = () => X12Parser.Parse(x12);
        act.Should().NotThrow();
    }

    // ── Map856Outbound ────────────────────────────────────────────────────────

    [Fact]
    public void Map856_ContainsBsnSegment()
    {
        var x12 = Build856();
        x12.Should().Contain("BSN*");
    }

    [Fact]
    public void Map856_ContainsHlShipmentLevel()
    {
        var x12 = Build856();
        x12.Should().Contain("*S*");
    }

    [Fact]
    public void Map856_ContainsTrackingNumber()
    {
        var x12 = Build856();
        x12.Should().Contain("TRACK-12345");
    }

    [Fact]
    public void Map856_IsParseable()
    {
        var x12 = Build856();
        var act = () => X12Parser.Parse(x12);
        act.Should().NotThrow();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string BuildSample997()
    {
        var inbound = X12Parser.Parse(SampleEdi.Edi850);
        var partner = new Web.Models.EDI.EdpPartner
        {
            EdpId  = "ACME",
            EdpIsa = "ACMEPARTNER",
            EdpGs  = "ACMEPARTNER",
            EdpActive = true
        };
        return Map997Outbound.Build(inbound, "A", partner, "ZAFFREMELD     ", "ZAFFREMELD");
    }

    private static string Build810()
    {
        var invoice = new Web.Models.Finance.ArMstr
        {
            ArId      = "INV-001",
            ArCust    = "ACME",
            ArAmt     = 499.95m,
            ArEntdate = "2026-03-01",
            ArRef     = "PO-98765"
        };
        var lines = new List<Web.Models.Finance.ArdMstr>
        {
            new() { ArdId = "INV-001", ArdLine = 1, ArdItem = "WIDGET-100", ArdAmt = 250.00m, ArdRef = "PO-98765" },
            new() { ArdId = "INV-001", ArdLine = 2, ArdItem = "GADGET-200", ArdAmt = 249.95m, ArdRef = "PO-98765" }
        };
        var partner = new Web.Models.EDI.EdpPartner
        {
            EdpId = "ACME", EdpIsa = "ACMEPARTNER", EdpGs = "ACMEPARTNER", EdpActive = true
        };
        return Map810Outbound.Build(invoice, lines, partner, "ZAFFREMELD     ", "ZAFFREMELD");
    }

    private static string Build856()
    {
        var ship = new Web.Models.Shipping.ShipMstr
        {
            ShId       = "SH-001",
            ShStatus   = "O",
            ShSite     = "DEFAULT",
            ShShipdate = "2026-03-04",
            ShCarrier  = "FEDEX",
            ShTrackno  = "TRACK-12345",
            ShWeight   = 25.5m
        };
        var lines = new List<Web.Models.Shipping.ShipDet>
        {
            new() { ShdId = "SH-001", ShdLine = 1, ShdItem = "WIDGET-100", ShdSo = "SO-001001", ShdQty = 10, ShdUom = "EA" },
            new() { ShdId = "SH-001", ShdLine = 2, ShdItem = "GADGET-200", ShdSo = "SO-001001", ShdQty = 5,  ShdUom = "EA" }
        };
        var partner = new Web.Models.EDI.EdpPartner
        {
            EdpId = "ACME", EdpIsa = "ACMEPARTNER", EdpGs = "ACMEPARTNER", EdpActive = true
        };
        return Map856Outbound.Build(ship, lines, partner, "ZAFFREMELD     ", "ZAFFREMELD");
    }
}
