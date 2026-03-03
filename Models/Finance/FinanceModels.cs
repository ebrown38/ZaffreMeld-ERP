using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Finance;

/// <summary>AcctMstr — Chart of accounts (gl_acct table)</summary>
public class AcctMstr
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    /// <summary>A=Asset, L=Liability, E=Equity, R=Revenue, X=Expense</summary>
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public bool CbDisplay { get; set; } = true;
    public string Site { get; set; } = "DEFAULT";
}

/// <summary>dept_mstr — Cost center / department master</summary>
public class DeptMstr
{
    [Key]
    public string DeptId { get; set; } = string.Empty;
    public string DeptDesc { get; set; } = string.Empty;
    public string DeptCopAcct { get; set; } = string.Empty;
    public string DeptSite { get; set; } = string.Empty;
    public bool DeptActive { get; set; } = true;
}

/// <summary>bank_mstr — Bank account master</summary>
public class BankMstr
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Routing { get; set; } = string.Empty;
    public string AssignedId { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public bool CbActive { get; set; } = true;
    public string GlAcct { get; set; } = string.Empty;
    public string GlCc { get; set; } = string.Empty;
}

/// <summary>curr_mstr — Currency master</summary>
public class CurrMstr
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 2;
    public bool IsBase { get; set; } = false;
}

/// <summary>exc_mstr — Currency exchange rates</summary>
public class ExcMstr
{
    public string ExcBase { get; set; } = string.Empty;
    public string ExcForeign { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExcRate { get; set; } = 1.0m;
    public DateTime ExcEffDate { get; set; } = DateTime.Today;
    public DateTime ExcUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>gl_ctrl — General ledger control parameters</summary>
public class GlCtrl
{
    [Key]
    public int Id { get; set; } = 1;
    public string GlBsFrom { get; set; } = string.Empty;
    public string GlBsTo { get; set; } = string.Empty;
    public string GlIsFrom { get; set; } = string.Empty;
    public string GlIsTo { get; set; } = string.Empty;
    public string GlRetainedEarnings { get; set; } = string.Empty;
    public string GlCurrentYear { get; set; } = string.Empty;
    public string GlCurrentPeriod { get; set; } = string.Empty;
    public string GlSite { get; set; } = "DEFAULT";
}

/// <summary>
/// gl_tran — General ledger transaction.
/// Central posting table for all financial activity.
/// </summary>
public class GlTran
{
    [Key]
    public int GltId { get; set; }
    public string GltRef { get; set; } = string.Empty;
    public string GltAcct { get; set; } = string.Empty;
    public string GltCc { get; set; } = string.Empty;
    public string GltEffdate { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal GltAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal GltBaseAmt { get; set; }
    public string GltCurr { get; set; } = "USD";
    public string GltBaseCurr { get; set; } = "USD";
    public string GltSite { get; set; } = string.Empty;
    /// <summary>JL=Journal, AP=Accounts Payable, AR=Accounts Receivable, IN=Invoice, SH=Shipping, RC=Receiving</summary>
    public string GltType { get; set; } = string.Empty;
    public string GltDesc { get; set; } = string.Empty;
    public string GltDoc { get; set; } = string.Empty;
    public string GltEntdate { get; set; } = string.Empty;
    public string GltUser { get; set; } = string.Empty;
    public string GltPeriod { get; set; } = string.Empty;
    public string GltYear { get; set; } = string.Empty;
    public bool GltPosted { get; set; } = false;
}

/// <summary>gl_hist — GL history (closed/reconciled transactions)</summary>
public class GlHist
{
    [Key]
    public int GlhId { get; set; }
    public string GlhRef { get; set; } = string.Empty;
    public string GlhAcct { get; set; } = string.Empty;
    public string GlhCc { get; set; } = string.Empty;
    public string GlhEffdate { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal GlhAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal GlhBaseAmt { get; set; }
    public string GlhCurr { get; set; } = "USD";
    public string GlhSite { get; set; } = string.Empty;
    public string GlhType { get; set; } = string.Empty;
    public string GlhDesc { get; set; } = string.Empty;
    public string GlhDoc { get; set; } = string.Empty;
    public string GlhPeriod { get; set; } = string.Empty;
    public string GlhYear { get; set; } = string.Empty;
}

/// <summary>gl_pair — Balanced GL debit/credit pair for posting</summary>
public class GlPair
{
    [Key]
    public int Id { get; set; }
    public string GlvAcctCr { get; set; } = string.Empty;
    public string GlvCcCr { get; set; } = string.Empty;
    public string GlvAcctDr { get; set; } = string.Empty;
    public string GlvCcDr { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal GlvAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal GlvBaseAmt { get; set; }
    public string GlvCurr { get; set; } = "USD";
    public string GlvEffdate { get; set; } = string.Empty;
    public string GlvRef { get; set; } = string.Empty;
    public string GlvType { get; set; } = string.Empty;
    public string GlvDesc { get; set; } = string.Empty;
    public string GlvDoc { get; set; } = string.Empty;
    public string GlvSite { get; set; } = string.Empty;
}

/// <summary>tax_mstr — Tax code master</summary>
public class TaxMstr
{
    [Key]
    public string TaxCode { get; set; } = string.Empty;
    public string TaxDesc { get; set; } = string.Empty;
    public string TaxCrtdate { get; set; } = string.Empty;
    public string TaxType { get; set; } = string.Empty;
    public bool TaxActive { get; set; } = true;
    public string TaxSite { get; set; } = string.Empty;
}

/// <summary>taxd_mstr — Tax detail lines (children of tax_mstr)</summary>
public class TaxdMstr
{
    public string TaxdParentcode { get; set; } = string.Empty;
    public string TaxdId { get; set; } = string.Empty;
    public string TaxdDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(8,4)")]
    public decimal TaxdPercent { get; set; }
    public string TaxdAcct { get; set; } = string.Empty;
    public string TaxdCc { get; set; } = string.Empty;
    public string TaxdType { get; set; } = string.Empty;
    public bool TaxdActive { get; set; } = true;
}

/// <summary>pay_ctrl — Payroll GL control accounts</summary>
public class PayCtrl
{
    [Key]
    public string PaycBank { get; set; } = string.Empty;
    public string PaycLaborAcct { get; set; } = string.Empty;
    public string PaycLaborCc { get; set; } = string.Empty;
    public string PaycTaxAcct { get; set; } = string.Empty;
    public string PaycTaxCc { get; set; } = string.Empty;
    public string PaycSite { get; set; } = string.Empty;
}

// ── Accounts Payable ──────────────────────────────────────────────────────────

/// <summary>ap_mstr — Accounts Payable master (invoice header)</summary>
public class ApMstr
{
    [Key]
    public string ApId { get; set; } = string.Empty;
    public string ApVend { get; set; } = string.Empty;
    public string ApNbr { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ApAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ApBaseAmt { get; set; }
    public string ApCurr { get; set; } = "USD";
    public string ApSite { get; set; } = string.Empty;
    public string ApEntdate { get; set; } = string.Empty;
    public string ApDuedate { get; set; } = string.Empty;
    public string ApStatus { get; set; } = "O";
    public string ApType { get; set; } = string.Empty;
    public string ApRef { get; set; } = string.Empty;
    public string ApDesc { get; set; } = string.Empty;
    public bool ApPosted { get; set; } = false;
}

/// <summary>apd_mstr — AP detail lines</summary>
public class ApdMstr
{
    [Key]
    public string ApdId { get; set; } = string.Empty;
    public string ApdBatch { get; set; } = string.Empty;
    public string ApdVend { get; set; } = string.Empty;
    public string ApdNbr { get; set; } = string.Empty;
    public int ApdLine { get; set; }
    public string ApdAcct { get; set; } = string.Empty;
    public string ApdCc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ApdAmt { get; set; }
    public string ApdDesc { get; set; } = string.Empty;
    public string ApdTaxcode { get; set; } = string.Empty;
}

/// <summary>vod_mstr — Voucher/AP open item detail</summary>
public class VodMstr
{
    [Key]
    public string VodId { get; set; } = string.Empty;
    public string VodRvdid { get; set; } = string.Empty;
    public int VodRvdline { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal VodAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal VodBaseAmt { get; set; }
    public string VodStatus { get; set; } = "O";
    public string VodApid { get; set; } = string.Empty;
    public string VodPo { get; set; } = string.Empty;
}

/// <summary>ap_ctrl — Accounts payable control record</summary>
public class ApCtrl
{
    [Key]
    public string ApcSite { get; set; } = string.Empty;
    public string ApcBank { get; set; } = string.Empty;
    public string ApcAssetacct { get; set; } = string.Empty;
    public string ApcAssetcc { get; set; } = string.Empty;
    public string ApcAutoVouch { get; set; } = "0";
    public string ApcVenditem { get; set; } = "0";
}

// ── Accounts Receivable ───────────────────────────────────────────────────────

/// <summary>ar_mstr — Accounts Receivable master (invoice header)</summary>
public class ArMstr
{
    [Key]
    public string ArId { get; set; } = string.Empty;
    public string ArNbr { get; set; } = string.Empty;
    public string ArCust { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ArAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ArBaseAmt { get; set; }
    public string ArCurr { get; set; } = "USD";
    public string ArSite { get; set; } = string.Empty;
    public string ArEntdate { get; set; } = string.Empty;
    public string ArDuedate { get; set; } = string.Empty;
    public string ArStatus { get; set; } = "O";
    public string ArType { get; set; } = string.Empty;
    public string ArShip { get; set; } = string.Empty;
    public string ArRef { get; set; } = string.Empty;
    public bool ArPosted { get; set; } = false;
}

/// <summary>ard_mstr — AR detail lines</summary>
public class ArdMstr
{
    [Key]
    public string ArdId { get; set; } = string.Empty;
    public string ArdNbr { get; set; } = string.Empty;
    public int ArdLine { get; set; }
    public string ArdCust { get; set; } = string.Empty;
    public string ArdRef { get; set; } = string.Empty;
    public string ArdItem { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")]
    public decimal ArdAmt { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ArdQty { get; set; }
    public string ArdUom { get; set; } = string.Empty;
    public string ArdAcct { get; set; } = string.Empty;
    public string ArdCc { get; set; } = string.Empty;
    public string ArdTaxcode { get; set; } = string.Empty;
    public string ArdDesc { get; set; } = string.Empty;
}

/// <summary>ar_ctrl — AR control record</summary>
public class ArCtrl
{
    [Key]
    public string ArcSite { get; set; } = string.Empty;
    public string ArcBank { get; set; } = string.Empty;
    public string ArcDefaultAcct { get; set; } = string.Empty;
    public string ArcDefaultCc { get; set; } = string.Empty;
    public string ArcAutoInvoice { get; set; } = "0";
}
