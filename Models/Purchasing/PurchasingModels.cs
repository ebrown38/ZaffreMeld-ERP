using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Purchasing;

public class PoMstr
{
    [Key] public string PoNbr { get; set; } = string.Empty;
    public string PoVend { get; set; } = string.Empty;
    public string PoSite { get; set; } = string.Empty;
    public string PoEntdate { get; set; } = string.Empty;
    public string PoReqdate { get; set; } = string.Empty;
    public string PoStatus { get; set; } = "O";
    public string PoType { get; set; } = "P";
    public string PoCurr { get; set; } = "USD";
    public string PoTerms { get; set; } = string.Empty;
    public string PoCarrier { get; set; } = string.Empty;
    public string PoNote { get; set; } = string.Empty;
    public string PoUser { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal PoTotalamt { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal PoTaxamt { get; set; } = 0;
    public string PoTaxcode { get; set; } = string.Empty;
    public bool PoPosted { get; set; } = false;
    public string PoShipvia { get; set; } = string.Empty;
    public string PoRevision { get; set; } = string.Empty;
}

public class PodMstr
{
    public string PodNbr { get; set; } = string.Empty;
    public int PodLine { get; set; }
    public string PodItem { get; set; } = string.Empty;
    public string PodVenditem { get; set; } = string.Empty;
    public string PodDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal PodQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal PodQtyrcv { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal PodPrice { get; set; } = 0;
    public string PodUom { get; set; } = "EA";
    public string PodReqdate { get; set; } = string.Empty;
    public string PodStatus { get; set; } = "O";
    public string PodAcct { get; set; } = string.Empty;
    public string PodCc { get; set; } = string.Empty;
    public string PodNote { get; set; } = string.Empty;
    public string PodWh { get; set; } = string.Empty;
    public string PodLoc { get; set; } = string.Empty;
}

public class PoAddr
{
    [Key] public string PoaNbr { get; set; } = string.Empty;
    public string PoaCode { get; set; } = string.Empty;
    public string PoaShipto { get; set; } = string.Empty;
    public string PoaName { get; set; } = string.Empty;
    public string PoaLine1 { get; set; } = string.Empty;
    public string PoaLine2 { get; set; } = string.Empty;
    public string PoaCity { get; set; } = string.Empty;
    public string PoaState { get; set; } = string.Empty;
    public string PoaZip { get; set; } = string.Empty;
    public string PoaCountry { get; set; } = string.Empty;
}

public class PoMeta
{
    [Key] public int Id { get; set; }
    public string PomNbr { get; set; } = string.Empty;
    public string PomDesc { get; set; } = string.Empty;
    public string PomType { get; set; } = string.Empty;
    public string PomValue { get; set; } = string.Empty;
}

public class PoCtrl
{
    [Key] public string PocSite { get; set; } = string.Empty;
    public string PocRcptAcct { get; set; } = string.Empty;
    public string PocRcptCc { get; set; } = string.Empty;
    public string PocVenditem { get; set; } = "0";
    public string PocRawonly { get; set; } = "0";
    public string PocAutoVouch { get; set; } = "0";
    public string PocAutoClose { get; set; } = "0";
}

public class PoTax
{
    [Key] public int Id { get; set; }
    public string PotNbr { get; set; } = string.Empty;
    public string PotDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(8,4)")] public decimal PotPercent { get; set; } = 0;
    public string PotType { get; set; } = string.Empty;
}

public class PodTax
{
    [Key] public int Id { get; set; }
    public string PodtNbr { get; set; } = string.Empty;
    public string PodtLine { get; set; } = string.Empty;
    public string PodtDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal PodtAmt { get; set; } = 0;
    public string PodtType { get; set; } = string.Empty;
}
