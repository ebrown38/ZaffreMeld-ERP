using System.ComponentModel.DataAnnotations;

namespace ZaffreMeld.Web.Models.EDI;

public class EdiXref
{
    [Key] public int Id { get; set; }
    public string ExrBsgs { get; set; } = string.Empty;
    public string ExrTpaddr { get; set; } = string.Empty;
    public string ExrBsaddr { get; set; } = string.Empty;
    public string ExrType { get; set; } = string.Empty;
    public bool ExrActive { get; set; } = true;
}

public class EdpPartner
{
    [Key] public string EdpId { get; set; } = string.Empty;
    public string EdpDesc { get; set; } = string.Empty;
    public string EdpSite { get; set; } = string.Empty;
    public string EdpType { get; set; } = string.Empty;
    public string EdpIsa { get; set; } = string.Empty;
    public string EdpGs { get; set; } = string.Empty;
    public bool EdpActive { get; set; } = true;
    public string EdpFtpid { get; set; } = string.Empty;
    public string EdpAs2id { get; set; } = string.Empty;
    public string EdpNote { get; set; } = string.Empty;
}

public class EdiDoc
{
    [Key] public string EddId { get; set; } = string.Empty;
    public string EddDesc { get; set; } = string.Empty;
    public string EddType { get; set; } = string.Empty;
    public string EddPartner { get; set; } = string.Empty;
    public string EddMap { get; set; } = string.Empty;
    public string EddDir { get; set; } = string.Empty;
    public bool EddActive { get; set; } = true;
}

public class EdiDocdet
{
    public string EdidId { get; set; } = string.Empty;
    public string EdidRole { get; set; } = string.Empty;
    public string EdidRectype { get; set; } = string.Empty;
    public string EdidDesc { get; set; } = string.Empty;
    public int EdidSeq { get; set; }
    public string EdidValue { get; set; } = string.Empty;
}

public class EdiMstr
{
    [Key] public int Id { get; set; }
    public string EdiId { get; set; } = string.Empty;
    public string EdiDoc2 { get; set; } = string.Empty;
    public string EdiSndisa { get; set; } = string.Empty;
    public string EdiSndq { get; set; } = string.Empty;
    public string EdiRcvisa { get; set; } = string.Empty;
    public string EdiRcvq { get; set; } = string.Empty;
    public string EdiStatus { get; set; } = string.Empty;
    public string EdiSite { get; set; } = string.Empty;
    public string EdiPartner { get; set; } = string.Empty;
    public DateTime EdiTimestamp { get; set; } = DateTime.UtcNow;
    public string EdiDir { get; set; } = string.Empty;
}
