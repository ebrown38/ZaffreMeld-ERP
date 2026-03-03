using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.HR;

public class EmpMstr
{
    [Key] public string EmpNbr { get; set; } = string.Empty;
    public string EmpLname { get; set; } = string.Empty;
    public string EmpFname { get; set; } = string.Empty;
    public string EmpMiddle { get; set; } = string.Empty;
    public string EmpDept { get; set; } = string.Empty;
    /// <summary>H=Hourly, S=Salary</summary>
    public string EmpType { get; set; } = "H";
    [Column(TypeName = "decimal(10,4)")] public decimal EmpRate { get; set; } = 0;
    public string EmpStatus { get; set; } = "A";
    public string EmpHiredate { get; set; } = string.Empty;
    public string EmpTermdate { get; set; } = string.Empty;
    public string EmpSite { get; set; } = string.Empty;
    public string EmpWcell { get; set; } = string.Empty;
    public string EmpNote { get; set; } = string.Empty;
    public string EmpUser { get; set; } = string.Empty;
    public string EmpShift { get; set; } = "1";
}

public class EmpException
{
    [Key] public int Id { get; set; }
    public string EmpxNbr { get; set; } = string.Empty;
    public string EmpxDesc { get; set; } = string.Empty;
    public string EmpxType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,4)")] public decimal EmpxHours { get; set; } = 0;
    public string EmpxDate { get; set; } = string.Empty;
    public string EmpxAcct { get; set; } = string.Empty;
    public string EmpxCc { get; set; } = string.Empty;
    public bool EmpxApproved { get; set; } = false;
}
