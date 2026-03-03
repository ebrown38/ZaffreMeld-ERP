using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Inventory;

/// <summary>item_mstr — Item master (parts/products)</summary>
public class ItemMstr
{
    [Key]
    public string ItItem { get; set; } = string.Empty;
    public string ItDesc { get; set; } = string.Empty;
    public int ItLotsize { get; set; } = 1;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ItLeadtime { get; set; } = 0;
    public string ItSite { get; set; } = string.Empty;
    public string ItUom { get; set; } = "EA";
    public string ItType { get; set; } = "M";  // M=Mfg, P=Purchase, S=Service
    public string ItStatus { get; set; } = "A";
    public string ItBom { get; set; } = string.Empty;
    public string ItRoute { get; set; } = string.Empty;
    public string ItProdline { get; set; } = string.Empty;
    public string ItGroup { get; set; } = string.Empty;
    public string ItWh { get; set; } = string.Empty;
    public string ItLoc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ItQoh { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ItSafetystock { get; set; } = 0;
    public string ItDrawing { get; set; } = string.Empty;
    public string ItRevision { get; set; } = string.Empty;
    public string ItAbc { get; set; } = "C";
    public string ItCrtdate { get; set; } = string.Empty;
    public string ItNote { get; set; } = string.Empty;
    public bool ItActive { get; set; } = true;
    public bool ItLottrk { get; set; } = false;
    public bool ItSerialtrk { get; set; } = false;
    public string ItPoAcct { get; set; } = string.Empty;
    public string ItPoCc { get; set; } = string.Empty;
    public string ItSalesAcct { get; set; } = string.Empty;
    public string ItSalesCc { get; set; } = string.Empty;
    public string ItCogAcct { get; set; } = string.Empty;
    public string ItCogCc { get; set; } = string.Empty;
}

/// <summary>item_cost — Item costing records</summary>
public class ItemCost
{
    public string ItcItem { get; set; } = string.Empty;
    public string ItcSite { get; set; } = string.Empty;
    public string ItcSet { get; set; } = "STD";
    [Column(TypeName = "decimal(18,6)")]
    public decimal ItcMatcost { get; set; } = 0;
    [Column(TypeName = "decimal(18,6)")]
    public decimal ItcLabcost { get; set; } = 0;
    [Column(TypeName = "decimal(18,6)")]
    public decimal ItcOvhcost { get; set; } = 0;
    [Column(TypeName = "decimal(18,6)")]
    public decimal ItcBurdcost { get; set; } = 0;
    [Column(TypeName = "decimal(18,6)")]
    public decimal ItcTotalcost { get; set; } = 0;
    public string ItcCurrency { get; set; } = "USD";
    public string ItcEffdate { get; set; } = string.Empty;
}

/// <summary>bom_mstr — Bill of Materials master</summary>
public class BomMstr
{
    [Key]
    public string BomId { get; set; } = string.Empty;
    public string BomDesc { get; set; } = string.Empty;
    public string BomSite { get; set; } = string.Empty;
    public string BomRevision { get; set; } = string.Empty;
    public string BomStatus { get; set; } = "A";
    public string BomNote { get; set; } = string.Empty;
    public string BomCrtdate { get; set; } = string.Empty;
}

/// <summary>pbm_mstr — Product/BOM structure (parent-child relationships)</summary>
public class PbmMstr
{
    public string PsParent { get; set; } = string.Empty;
    public string PsChild { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal PsQty { get; set; } = 1;
    public string PsUom { get; set; } = "EA";
    public int PsSeq { get; set; } = 10;
    public string PsType { get; set; } = "C";  // C=Component, R=Reference
    public bool PsActive { get; set; } = true;
    public string PsNote { get; set; } = string.Empty;
    public string PsEfffrom { get; set; } = string.Empty;
    public string PsEffthru { get; set; } = string.Empty;
    public string PsRevision { get; set; } = string.Empty;
}

/// <summary>wf_mstr — Work flow/routing master</summary>
public class WfMstr
{
    [Key]
    public string WfId { get; set; } = string.Empty;
    public string WfDesc { get; set; } = string.Empty;
    public string WfSite { get; set; } = string.Empty;
    public string WfStatus { get; set; } = "A";
    public string WfNote { get; set; } = string.Empty;
}

/// <summary>wc_mstr — Work center master</summary>
public class WcMstr
{
    [Key]
    public string WcCell { get; set; } = string.Empty;
    public string WcDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(8,2)")]
    public decimal WcRate { get; set; } = 0;
    [Column(TypeName = "decimal(8,2)")]
    public decimal WcBurdrate { get; set; } = 0;
    public string WcSite { get; set; } = string.Empty;
    public bool WcActive { get; set; } = true;
    public string WcAcct { get; set; } = string.Empty;
    public string WcCc { get; set; } = string.Empty;
}

/// <summary>loc_mstr — Warehouse location master</summary>
public class LocMstr
{
    [Key]
    public string LocLoc { get; set; } = string.Empty;
    public string LocDesc { get; set; } = string.Empty;
    public string LocSite { get; set; } = string.Empty;
    public string LocWh { get; set; } = string.Empty;
    public string LocType { get; set; } = string.Empty;
    public bool LocActive { get; set; } = true;
    public bool LocPickable { get; set; } = true;
    public bool LocReceivable { get; set; } = true;
}

/// <summary>uom_mstr — Unit of measure master</summary>
public class UomMstr
{
    [Key]
    public string UomId { get; set; } = string.Empty;
    public string UomDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,6)")]
    public decimal UomConvFactor { get; set; } = 1;
    public string UomBaseUom { get; set; } = string.Empty;
    public bool UomActive { get; set; } = true;
}

/// <summary>wh_mstr — Warehouse master</summary>
public class WhMstr
{
    [Key]
    public string WhId { get; set; } = string.Empty;
    public string WhSite { get; set; } = string.Empty;
    public string WhDesc { get; set; } = string.Empty;
    public string WhName { get; set; } = string.Empty;
    public string WhLine1 { get; set; } = string.Empty;
    public string WhLine2 { get; set; } = string.Empty;
    public string WhCity { get; set; } = string.Empty;
    public string WhState { get; set; } = string.Empty;
    public string WhZip { get; set; } = string.Empty;
    public string WhCountry { get; set; } = string.Empty;
    public bool WhActive { get; set; } = true;
    public bool WhDefault { get; set; } = false;
}
