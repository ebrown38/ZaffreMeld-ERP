using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Production;

public class JobClock
{
    [Key] public int Id { get; set; }
    public int JobcPlanid { get; set; }
    public int JobcOp { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal JobcQty { get; set; } = 0;
    public string JobcEmpnbr { get; set; } = string.Empty;
    public string JobcDate { get; set; } = string.Empty;
    public string JobcTimein { get; set; } = string.Empty;
    public string JobcTimeout { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,4)")] public decimal JobcHours { get; set; } = 0;
    public string JobcSite { get; set; } = string.Empty;
    public bool JobcPosted { get; set; } = false;
}
