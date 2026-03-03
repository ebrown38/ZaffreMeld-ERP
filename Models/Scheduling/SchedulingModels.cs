using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Scheduling;

public class PlanMstr
{
    [Key] public int PlanNbr { get; set; }
    public string PlanItem { get; set; } = string.Empty;
    public string PlanSite { get; set; } = string.Empty;
    public string PlanStatus { get; set; } = "O";
    /// <summary>W=Work Order, F=Firm Planned, P=Planned</summary>
    public string PlanType { get; set; } = "W";
    [Column(TypeName = "decimal(18,4)")] public decimal PlanQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal PlanQtycomp { get; set; } = 0;
    public string PlanStartdate { get; set; } = string.Empty;
    public string PlanDuedate { get; set; } = string.Empty;
    public string PlanSo { get; set; } = string.Empty;
    public string PlanSoline { get; set; } = string.Empty;
    public string PlanNote { get; set; } = string.Empty;
    public string PlanUser { get; set; } = string.Empty;
    public bool PlanPosted { get; set; } = false;
    public string PlanBom { get; set; } = string.Empty;
    public string PlanRoute { get; set; } = string.Empty;
}

public class PlanOperation
{
    [Key] public int PloId { get; set; }
    public int PloParent { get; set; }
    public int PloSeq { get; set; }
    public string PloCellid { get; set; } = string.Empty;
    public string PloDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,4)")] public decimal PloSetuptime { get; set; } = 0;
    [Column(TypeName = "decimal(10,4)")] public decimal PloRuntime { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal PloQtycomp { get; set; } = 0;
    public string PloStatus { get; set; } = "O";
    public string PloStartdate { get; set; } = string.Empty;
    public string PloDuedate { get; set; } = string.Empty;
}
