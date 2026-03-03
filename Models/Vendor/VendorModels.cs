using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Vendor;

public class VdMstr
{
    [Key] public string VdAddr { get; set; } = string.Empty;
    public string VdSite { get; set; } = string.Empty;
    public string VdName { get; set; } = string.Empty;
    public string VdLine1 { get; set; } = string.Empty;
    public string VdLine2 { get; set; } = string.Empty;
    public string VdCity { get; set; } = string.Empty;
    public string VdState { get; set; } = string.Empty;
    public string VdZip { get; set; } = string.Empty;
    public string VdCountry { get; set; } = string.Empty;
    public string VdPhone { get; set; } = string.Empty;
    public string VdFax { get; set; } = string.Empty;
    public string VdEmail { get; set; } = string.Empty;
    public string VdContact { get; set; } = string.Empty;
    public string VdTerms { get; set; } = string.Empty;
    public string VdCurrency { get; set; } = "USD";
    public string VdStatus { get; set; } = "A";
    public string VdNote { get; set; } = string.Empty;
    public string VdType { get; set; } = string.Empty;
    public string VdTaxid { get; set; } = string.Empty;
    public string VdPayacct { get; set; } = string.Empty;
    public string VdPaycc { get; set; } = string.Empty;
    public string VdCrtdate { get; set; } = string.Empty;
}

public class VdCtrl
{
    [Key] public string VdcSite { get; set; } = string.Empty;
    public string VdcAutovend { get; set; } = "0";
    public string VdcDefaultTerms { get; set; } = string.Empty;
    public string VdcDefaultCurrency { get; set; } = "USD";
}

public class VdpMstr
{
    public string VdpVend { get; set; } = string.Empty;
    public string VdpItem { get; set; } = string.Empty;
    public string VdpVitem { get; set; } = string.Empty;
    public string VdpVitem2 { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,6)")] public decimal VdpConvfactor { get; set; } = 1;
    public string VdpUom { get; set; } = string.Empty;
    public bool VdpActive { get; set; } = true;
}

public class VprMstr
{
    [Key] public int Id { get; set; }
    public string VprVend { get; set; } = string.Empty;
    public string VprItem { get; set; } = string.Empty;
    public string VprType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal VprPrice { get; set; } = 0;
    public string VprUom { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal VprMinqty { get; set; } = 0;
    public string VprCurrency { get; set; } = "USD";
    public string VprEfffrom { get; set; } = string.Empty;
    public string VprEffthru { get; set; } = string.Empty;
    public bool VprActive { get; set; } = true;
}

public class VdsDet
{
    public string VdsCode { get; set; } = string.Empty;
    public string VdsShipto { get; set; } = string.Empty;
    public string VdsName { get; set; } = string.Empty;
    public string VdsLine1 { get; set; } = string.Empty;
    public string VdsCity { get; set; } = string.Empty;
    public string VdsState { get; set; } = string.Empty;
    public string VdsZip { get; set; } = string.Empty;
    public string VdsCountry { get; set; } = string.Empty;
}
