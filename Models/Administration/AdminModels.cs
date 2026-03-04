using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ZaffreMeld.Web.Models.Administration;

/// <summary>
/// ASP.NET Core Identity user — extends the original Java user_mstr table.
/// The Identity system handles password hashing, claims, and token generation.
/// </summary>
public class ZaffreMeldUser : IdentityUser
{
    /// <summary>user_id from original user_mstr</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>user_site — the site this user belongs to</summary>
    public string UserSite { get; set; } = "DEFAULT";

    /// <summary>user_lname</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>user_fname</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>user_email</summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>user_phone</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>user_active — 1=active, 0=inactive</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>user_type — admin, user, api</summary>
    public string UserType { get; set; } = "user";

    /// <summary>Preferred locale / language</summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>Last login timestamp</summary>
    public DateTime? LastLogin { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// ASP.NET Core Identity role — maps to ZaffreMeld permission groups.
/// </summary>
public class ZaffreMeldRole : IdentityRole
{
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; } = false;
}

/// <summary>
/// Original user_mstr table entity (kept for direct DB queries that need it).
/// </summary>
public class UserMstr
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    public string UserSite { get; set; } = string.Empty;
    public string UserLname { get; set; } = string.Empty;
    public string UserFname { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserPass { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string UserActive { get; set; } = "1";
    public string UserGroup { get; set; } = string.Empty;
    public string UserLocale { get; set; } = string.Empty;
}

/// <summary>
/// Site master — site_mstr in the original Java code.
/// Multi-site ERP support.
/// </summary>
public class SiteMstr
{
    public int  SiteMstrId { get; set; }
    public string SiteSite { get; set; } = string.Empty;
    public string SiteDesc { get; set; } = string.Empty;
    public string SiteLine1 { get; set; } = string.Empty;
    public string SiteLine2 { get; set; } = string.Empty;
    public string SiteCity { get; set; } = string.Empty;
    public string SiteState { get; set; } = string.Empty;
    public string SiteZip { get; set; } = string.Empty;
    public string SiteCountry { get; set; } = string.Empty;
    public string SitePhone { get; set; } = string.Empty;
    public string SiteFax { get; set; } = string.Empty;
    public string SiteCurrency { get; set; } = "USD";
    public string SiteActive { get; set; } = "1";
}

/// <summary>ov_mstr — Overall/overhead master control record</summary>
public class OvMstr
{
    public int OvMstrId { get; set; }
    public string OvSite { get; set; } = string.Empty;
    public string OvCc { get; set; } = string.Empty;
    public string OvWh { get; set; } = string.Empty;
    public string OvGlacct { get; set; } = string.Empty;
    public string OvTaxcode { get; set; } = string.Empty;
}

/// <summary>ov_ctrl — Overall system control parameters</summary>
public class OvCtrl
{
    public int OvCtrlId { get; set; }
    public string OvVersion { get; set; } = string.Empty;
    public string OvDistDir { get; set; } = string.Empty;
    public string OvSourceDir { get; set; } = string.Empty;
    public string OvSite { get; set; } = string.Empty;
    public string OvCurrency { get; set; } = "USD";
    public string OvLocale { get; set; } = "en-US";
    public string OvDecimalSep { get; set; } = ".";
    public int OvDecimalPlaces { get; set; } = 2;
}

/// <summary>code_mstr — Code/lookup table (replaces Java code_mstr)</summary>
public class CodeMstr
{
    public int CodeMstrId { get; set; }
    public string CodeCode { get; set; } = string.Empty;
    public string CodeKey { get; set; } = string.Empty;
    public string CodeValue { get; set; } = string.Empty;
    public string CodeDesc { get; set; } = string.Empty;
    public string CodeActive { get; set; } = "1";
}

/// <summary>menu_mstr — Menu definition for navigation</summary>
public class MenuMstr
{
    [Key]
    public string MenuId { get; set; } = string.Empty;
    public string MenuDesc { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string MenuParent { get; set; } = string.Empty;
    public string MenuProgram { get; set; } = string.Empty;
    public int MenuSeq { get; set; }
    public string MenuIcon { get; set; } = string.Empty;
    public string MenuActive { get; set; } = "1";
    public string MenuRole { get; set; } = string.Empty;
}

/// <summary>counter — Document number counter (SO, PO, invoice numbers etc.)</summary>
public class Counter
{
    public int CounterId { get; set; }
    public string CounterName { get; set; } = string.Empty;
    public string CounterDesc { get; set; } = string.Empty;
    public string CounterPrefix { get; set; } = string.Empty;
    public int CounterValue { get; set; } = 0;
    public int CounterLength { get; set; } = 7;
    public string CounterSite { get; set; } = string.Empty;
}

/// <summary>ftp_mstr — FTP/SFTP connection profiles</summary>
public class FtpMstr
{
    [Key]
    public string FtpId { get; set; } = string.Empty;
    public string FtpDesc { get; set; } = string.Empty;
    public string FtpIp { get; set; } = string.Empty;
    public string FtpPort { get; set; } = "21";
    public string FtpUser { get; set; } = string.Empty;
    public string FtpPass { get; set; } = string.Empty;
    public string FtpType { get; set; } = "ftp";
    public string FtpDir { get; set; } = string.Empty;
    public string FtpActive { get; set; } = "1";
}

/// <summary>ftp_attr — Key/value attributes for FTP profiles</summary>
public class FtpAttr
{
    public string FtpaId { get; set; } = string.Empty;
    public string FtpaKey { get; set; } = string.Empty;
    public string FtpaValue { get; set; } = string.Empty;
}

/// <summary>change_log — Audit trail for data changes</summary>
public class ChangeLog
{
    public int Id { get; set; }
    public string ClUser { get; set; } = string.Empty;
    public string ClSite { get; set; } = string.Empty;
    public string ClTable { get; set; } = string.Empty;
    public string ClKey { get; set; } = string.Empty;
    public string ClAction { get; set; } = string.Empty;
    public string ClField { get; set; } = string.Empty;
    public string ClOldValue { get; set; } = string.Empty;
    public string ClNewValue { get; set; } = string.Empty;
    public DateTime ClTimestamp { get; set; } = DateTime.UtcNow;
    public string ClIp { get; set; } = string.Empty;
}
