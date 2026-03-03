using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Distribution;

public class DoMstr
{
    [Key] public string DoNbr { get; set; } = string.Empty;
    public string DoSite { get; set; } = string.Empty;
    public string DoType { get; set; } = string.Empty;
    public string DoStatus { get; set; } = "O";
    public string DoEntdate { get; set; } = string.Empty;
    public string DoReqdate { get; set; } = string.Empty;
    public string DoFromsite { get; set; } = string.Empty;
    public string DoTosite { get; set; } = string.Empty;
    public string DoNote { get; set; } = string.Empty;
    public string DoUser { get; set; } = string.Empty;
}

public class DodMstr
{
    [Key] public int Id { get; set; }
    public string DodNbr { get; set; } = string.Empty;
    public string DodLine { get; set; } = string.Empty;
    public string DodItem { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal DodQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal DodQtyrcv { get; set; } = 0;
    public string DodUom { get; set; } = "EA";
    public string DodStatus { get; set; } = "O";
    public string DodNote { get; set; } = string.Empty;
}
