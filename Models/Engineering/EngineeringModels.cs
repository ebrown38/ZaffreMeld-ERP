using System.ComponentModel.DataAnnotations;

namespace ZaffreMeld.Web.Models.Engineering;

public class EcnMstr
{
    [Key] public string EcnNbr { get; set; } = string.Empty;
    public string EcnPoc { get; set; } = string.Empty;
    public string EcnMstrtask { get; set; } = string.Empty;
    public string EcnDesc { get; set; } = string.Empty;
    public string EcnStatus { get; set; } = "O";
    public string EcnSite { get; set; } = string.Empty;
    public string EcnCrtdate { get; set; } = string.Empty;
    public string EcnNote { get; set; } = string.Empty;
    public string EcnUser { get; set; } = string.Empty;
}

public class EcnTask
{
    public string EcntNbr { get; set; } = string.Empty;
    public string EcntMstrid { get; set; } = string.Empty;
    public string EcntSeq { get; set; } = string.Empty;
    public string EcntOwner { get; set; } = string.Empty;
    public string EcntStatus { get; set; } = "O";
    public string EcntDuedate { get; set; } = string.Empty;
    public string EcntNote { get; set; } = string.Empty;
}

public class TaskMstr
{
    [Key] public string TaskId { get; set; } = string.Empty;
    public string TaskDesc { get; set; } = string.Empty;
    public bool TaskActive { get; set; } = true;
}

public class TaskDet
{
    [Key] public int Id { get; set; }
    public string TaskdId { get; set; } = string.Empty;
    public string TaskdOwner { get; set; } = string.Empty;
    public string TaskdDesc { get; set; } = string.Empty;
    public string TaskdStatus { get; set; } = "O";
    public string TaskdDuedate { get; set; } = string.Empty;
    public string TaskdNote { get; set; } = string.Empty;
}
