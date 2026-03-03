using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Freight;

public class CarMstr
{
    [Key] public string CarId { get; set; } = string.Empty;
    public string CarDesc { get; set; } = string.Empty;
    public string CarApply { get; set; } = string.Empty;
    public string CarScaccode { get; set; } = string.Empty;
    public string CarType { get; set; } = string.Empty;
    public bool CarActive { get; set; } = true;
    public string CarSite { get; set; } = string.Empty;
    public string CarAcct { get; set; } = string.Empty;
    public string CarCc { get; set; } = string.Empty;
}

public class CfoMstr
{
    [Key] public string CfoNbr { get; set; } = string.Empty;
    public string CfoCust { get; set; } = string.Empty;
    public string CfoCustfonbr { get; set; } = string.Empty;
    public string CfoRevision { get; set; } = string.Empty;
    public string CfoStatus { get; set; } = "O";
    public string CfoSite { get; set; } = string.Empty;
    public string CfoEntdate { get; set; } = string.Empty;
    public string CfoNote { get; set; } = string.Empty;
    public string CfoUser { get; set; } = string.Empty;
}

public class CfoDet
{
    [Key] public int Id { get; set; }
    public string CfodNbr { get; set; } = string.Empty;
    public string CfodRevision { get; set; } = string.Empty;
    public string CfodStopline { get; set; } = string.Empty;
    public string CfodSeq { get; set; } = string.Empty;
    public string CfodType { get; set; } = string.Empty;
    public string CfodValue { get; set; } = string.Empty;
}

public class CfoItem
{
    [Key] public int Id { get; set; }
    public string CfoiNbr { get; set; } = string.Empty;
    public string CfoiRevision { get; set; } = string.Empty;
    public string CfoiStopline { get; set; } = string.Empty;
    public string CfoiItemline { get; set; } = string.Empty;
    public string CfoiItem { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal CfoiQty { get; set; } = 0;
    public string CfoiUom { get; set; } = "EA";
}

public class VehMstr
{
    [Key] public string VehId { get; set; } = string.Empty;
    public string VehDesc { get; set; } = string.Empty;
    public string VehType { get; set; } = string.Empty;
    public string VehPlate { get; set; } = string.Empty;
    public string VehVin { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,2)")] public decimal VehCapacity { get; set; } = 0;
    public bool VehActive { get; set; } = true;
    public string VehSite { get; set; } = string.Empty;
}

public class DrvMstr
{
    [Key] public string DrvId { get; set; } = string.Empty;
    public string DrvStatus { get; set; } = "A";
    public string DrvLname { get; set; } = string.Empty;
    public string DrvFname { get; set; } = string.Empty;
    public string DrvLicense { get; set; } = string.Empty;
    public string DrvPhone { get; set; } = string.Empty;
    public string DrvEmail { get; set; } = string.Empty;
    public bool DrvActive { get; set; } = true;
    public string DrvSite { get; set; } = string.Empty;
}
