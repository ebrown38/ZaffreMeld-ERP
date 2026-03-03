using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Orders;

// ── Customer Master ────────────────────────────────────────────────────────────

/// <summary>cm_mstr — Customer master</summary>
public class CmMstr
{
    [Key]
    public string CmCode { get; set; } = string.Empty;
    public string CmName { get; set; } = string.Empty;
    public string CmLine1 { get; set; } = string.Empty;
    public string CmLine2 { get; set; } = string.Empty;
    public string CmCity { get; set; } = string.Empty;
    public string CmState { get; set; } = string.Empty;
    public string CmZip { get; set; } = string.Empty;
    public string CmCountry { get; set; } = string.Empty;
    public string CmPhone { get; set; } = string.Empty;
    public string CmFax { get; set; } = string.Empty;
    public string CmEmail { get; set; } = string.Empty;
    public string CmTerms { get; set; } = string.Empty;
    public string CmCurrency { get; set; } = "USD";
    public string CmTaxcode { get; set; } = string.Empty;
    public string CmSite { get; set; } = string.Empty;
    public string CmCrtdate { get; set; } = string.Empty;
    public string CmSlsp { get; set; } = string.Empty;
    public string CmContact { get; set; } = string.Empty;
    public string CmNote { get; set; } = string.Empty;
    public string CmStatus { get; set; } = "A";
    public string CmType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal CmCreditlimit { get; set; } = 0;
    public string CmAcct { get; set; } = string.Empty;
    public string CmCc { get; set; } = string.Empty;
}

/// <summary>cms_det — Customer ship-to address</summary>
public class CmsDet
{
    public string CmsCode { get; set; } = string.Empty;
    public string CmsShipto { get; set; } = string.Empty;
    public string CmsName { get; set; } = string.Empty;
    public string CmsLine1 { get; set; } = string.Empty;
    public string CmsLine2 { get; set; } = string.Empty;
    public string CmsCity { get; set; } = string.Empty;
    public string CmsState { get; set; } = string.Empty;
    public string CmsZip { get; set; } = string.Empty;
    public string CmsCountry { get; set; } = string.Empty;
    public string CmsPhone { get; set; } = string.Empty;
    public string CmsContact { get; set; } = string.Empty;
    public bool CmsDefault { get; set; } = false;
}

/// <summary>cmc_det — Customer contact</summary>
public class CmcDet
{
    [Key]
    public string CmcId { get; set; } = string.Empty;
    public string CmcCode { get; set; } = string.Empty;
    public string CmcType { get; set; } = string.Empty;
    public string CmcName { get; set; } = string.Empty;
    public string CmcEmail { get; set; } = string.Empty;
    public string CmcPhone { get; set; } = string.Empty;
    public string CmcNote { get; set; } = string.Empty;
    public bool CmcActive { get; set; } = true;
}

/// <summary>cup_mstr — Customer cross-reference (customer's item# to our item#)</summary>
public class CupMstr
{
    public string CupCust { get; set; } = string.Empty;
    public string CupItem { get; set; } = string.Empty;
    public string CupCitem { get; set; } = string.Empty;
    public string CupCitem2 { get; set; } = string.Empty;
    public string CupUom { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,6)")]
    public decimal CupConvfactor { get; set; } = 1;
    public string CupNote { get; set; } = string.Empty;
    public bool CupActive { get; set; } = true;
}

/// <summary>cpr_mstr — Customer pricing</summary>
public class CprMstr
{
    [Key]
    public int Id { get; set; }
    public string CprCust { get; set; } = string.Empty;
    public string CprItem { get; set; } = string.Empty;
    public string CprType { get; set; } = string.Empty;  // P=Price, D=Discount
    public string CprDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal CprPrice { get; set; } = 0;
    public string CprUom { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal CprMinqty { get; set; } = 0;
    public string CprCurrency { get; set; } = "USD";
    public string CprEfffrom { get; set; } = string.Empty;
    public string CprEffthru { get; set; } = string.Empty;
    public bool CprActive { get; set; } = true;
}

/// <summary>cut_code — Customer payment terms</summary>
public class CustTerm
{
    [Key]
    public string CutCode { get; set; } = string.Empty;
    public string CutDesc { get; set; } = string.Empty;
    public int CutDays { get; set; } = 30;
    public int CutDiscdays { get; set; } = 0;
    [Column(TypeName = "decimal(6,4)")]
    public decimal CutDiscpct { get; set; } = 0;
    public string CutType { get; set; } = "N";
    public bool CutActive { get; set; } = true;
}

/// <summary>slsp_mstr — Salesperson master</summary>
public class SlspMstr
{
    [Key]
    public string SlspId { get; set; } = string.Empty;
    public string SlspName { get; set; } = string.Empty;
    public string SlspLine1 { get; set; } = string.Empty;
    public string SlspLine2 { get; set; } = string.Empty;
    public string SlspCity { get; set; } = string.Empty;
    public string SlspState { get; set; } = string.Empty;
    public string SlspZip { get; set; } = string.Empty;
    public string SlspPhone { get; set; } = string.Empty;
    public string SlspEmail { get; set; } = string.Empty;
    [Column(TypeName = "decimal(6,4)")]
    public decimal SlspCommrate { get; set; } = 0;
    public bool SlspActive { get; set; } = true;
    public string SlspSite { get; set; } = string.Empty;
}

/// <summary>cm_ctrl — Customer module control</summary>
public class CmCtrl
{
    [Key]
    public string CmcSite { get; set; } = string.Empty;
    public string CmcAutocust { get; set; } = "0";
    public string CmcDefaultTerms { get; set; } = string.Empty;
    public string CmcDefaultCurrency { get; set; } = "USD";
}

// ── Sales Orders ──────────────────────────────────────────────────────────────

/// <summary>so_mstr — Sales order master</summary>
public class SoMstr
{
    [Key]
    public string SoNbr { get; set; } = string.Empty;
    public string SoCust { get; set; } = string.Empty;
    public string SoShip { get; set; } = string.Empty;
    public string SoSite { get; set; } = string.Empty;
    public string SoEntdate { get; set; } = string.Empty;
    public string SoReqdate { get; set; } = string.Empty;
    public string SoStatus { get; set; } = "O";  // O=Open, C=Closed, H=Hold
    public string SoType { get; set; } = "S";
    public string SoPo { get; set; } = string.Empty;
    public string SoTerms { get; set; } = string.Empty;
    public string SoCurr { get; set; } = "USD";
    public string SoSlsp { get; set; } = string.Empty;
    public string SoCarrier { get; set; } = string.Empty;
    public string SoNote { get; set; } = string.Empty;
    public string SoUser { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SoTotalamt { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SoTaxamt { get; set; } = 0;
    public string SoTaxcode { get; set; } = string.Empty;
    public string SoFreight { get; set; } = string.Empty;
    public bool SoPosted { get; set; } = false;
    public string SoRevision { get; set; } = string.Empty;
}

/// <summary>sod_det — Sales order detail/line</summary>
public class SodDet
{
    public string SodNbr { get; set; } = string.Empty;
    public int SodLine { get; set; }
    public string SodItem { get; set; } = string.Empty;
    public string SodCustitem { get; set; } = string.Empty;
    public string SodDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SodQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SodQtyship { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SodPrice { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SodDisc { get; set; } = 0;
    public string SodUom { get; set; } = "EA";
    public string SodReqdate { get; set; } = string.Empty;
    public string SodStatus { get; set; } = "O";
    public string SodSite { get; set; } = string.Empty;
    public string SodWh { get; set; } = string.Empty;
    public string SodNote { get; set; } = string.Empty;
    public string SodAcct { get; set; } = string.Empty;
    public string SodCc { get; set; } = string.Empty;
    public string SodTaxcode { get; set; } = string.Empty;
}

/// <summary>so_tax — Sales order tax header</summary>
public class SoTax
{
    [Key]
    public int Id { get; set; }
    public string SotNbr { get; set; } = string.Empty;
    public string SotDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(8,4)")]
    public decimal SotPercent { get; set; } = 0;
    public string SotType { get; set; } = string.Empty;
}

/// <summary>sod_tax — Sales order line-level tax</summary>
public class SodTax
{
    [Key]
    public int Id { get; set; }
    public string SodtNbr { get; set; } = string.Empty;
    public string SodtLine { get; set; } = string.Empty;
    public string SodtDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SodtAmt { get; set; } = 0;
    public string SodtType { get; set; } = string.Empty;
}

/// <summary>sos_det — Sales order surcharges/fees</summary>
public class SosDet
{
    [Key]
    public int Id { get; set; }
    public string SosNbr { get; set; } = string.Empty;
    public string SosDesc { get; set; } = string.Empty;
    public string SosType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SosAmt { get; set; } = 0;
    public string SosAcct { get; set; } = string.Empty;
    public string SosCc { get; set; } = string.Empty;
}

// ── Service Orders ────────────────────────────────────────────────────────────

/// <summary>sv_mstr — Service order master</summary>
public class SvMstr
{
    [Key]
    public string SvNbr { get; set; } = string.Empty;
    public string SvCust { get; set; } = string.Empty;
    public string SvShip { get; set; } = string.Empty;
    public string SvPo { get; set; } = string.Empty;
    public string SvStatus { get; set; } = "O";
    public string SvSite { get; set; } = string.Empty;
    public string SvEntdate { get; set; } = string.Empty;
    public string SvReqdate { get; set; } = string.Empty;
    public string SvNote { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SvTotalamt { get; set; } = 0;
    public string SvCurr { get; set; } = "USD";
    public string SvUser { get; set; } = string.Empty;
}

/// <summary>svd_det — Service order detail</summary>
public class SvdDet
{
    [Key]
    public int Id { get; set; }
    public string SvdNbr { get; set; } = string.Empty;
    public int SvdLine { get; set; }
    public string SvdUom { get; set; } = string.Empty;
    public string SvdItem { get; set; } = string.Empty;
    public string SvdDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SvdQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal SvdPrice { get; set; } = 0;
    public string SvdStatus { get; set; } = "O";
}

// ── Point of Sale ─────────────────────────────────────────────────────────────

/// <summary>pos_mstr — POS transaction master</summary>
public class PosMstr
{
    [Key]
    public string PosNbr { get; set; } = string.Empty;
    public string PosEntrydate { get; set; } = string.Empty;
    public string PosEntrytime { get; set; } = string.Empty;
    public string PosCust { get; set; } = string.Empty;
    public string PosStatus { get; set; } = "O";
    public string PosSite { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal PosTotal { get; set; } = 0;
    public string PosUser { get; set; } = string.Empty;
    public string PosTender { get; set; } = string.Empty;
}

/// <summary>posd_det — POS transaction detail</summary>
public class PosDet
{
    [Key]
    public int Id { get; set; }
    public string PosdNbr { get; set; } = string.Empty;
    public string PosdLine { get; set; } = string.Empty;
    public string PosdItem { get; set; } = string.Empty;
    public string PosdDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal PosdQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal PosdPrice { get; set; } = 0;
    public string PosdUom { get; set; } = "EA";
}
