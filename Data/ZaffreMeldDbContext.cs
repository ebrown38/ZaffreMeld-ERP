using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Models.Orders;
using ZaffreMeld.Web.Models.Purchasing;
using ZaffreMeld.Web.Models.Shipping;
using ZaffreMeld.Web.Models.Vendor;
using ZaffreMeld.Web.Models.HR;
using ZaffreMeld.Web.Models.EDI;
using ZaffreMeld.Web.Models.Engineering;
using ZaffreMeld.Web.Models.Freight;
using ZaffreMeld.Web.Models.Production;
using ZaffreMeld.Web.Models.Scheduling;
using ZaffreMeld.Web.Models.Receiving;
using ZaffreMeld.Web.Models.Distribution;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Data;

/// <summary>
/// Main EF Core DbContext for ZaffreMeld ERP.
/// Replaces direct JDBC SQL connections from the Java source.
/// Each DbSet corresponds to a SQL table in the original zaffremeld schema.
/// </summary>
public class ZaffreMeldDbContext : IdentityDbContext<ZaffreMeldUser, ZaffreMeldRole, string>
{
    public ZaffreMeldDbContext(DbContextOptions<ZaffreMeldDbContext> options) : base(options) { }

    // ── Administration ────────────────────────────────────────────────────────
    public DbSet<SiteMstr> Sites { get; set; }
    public DbSet<ZaffreMeldUser> ZaffreMeldUsers { get; set; }
    public DbSet<OvMstr> OvMstr { get; set; }
    public DbSet<OvCtrl> OvCtrl { get; set; }
    public DbSet<CodeMstr> CodeMstr { get; set; }
    public DbSet<MenuMstr> MenuMstr { get; set; }
    public DbSet<Counter> Counters { get; set; }
    public DbSet<FtpMstr> FtpMstr { get; set; }
    public DbSet<FtpAttr> FtpAttrs { get; set; }
    public DbSet<ChangeLog> ChangeLogs { get; set; }

    // ── Finance / General Ledger ──────────────────────────────────────────────
    public DbSet<AcctMstr> AcctMstr { get; set; }
    public DbSet<DeptMstr> DeptMstr { get; set; }
    public DbSet<BankMstr> BankMstr { get; set; }
    public DbSet<CurrMstr> CurrMstr { get; set; }
    public DbSet<ExcMstr> ExcMstr { get; set; }
    public DbSet<GlCtrl> GlCtrl { get; set; }
    public DbSet<GlTran> GlTran { get; set; }
    public DbSet<GlHist> GlHist { get; set; }
    public DbSet<GlPair> GlPair { get; set; }
    public DbSet<TaxMstr> TaxMstr { get; set; }
    public DbSet<TaxdMstr> TaxdMstr { get; set; }
    public DbSet<PayCtrl> PayCtrl { get; set; }

    // ── Accounts Payable ──────────────────────────────────────────────────────
    public DbSet<ApMstr> ApMstr { get; set; }
    public DbSet<ApdMstr> ApdMstr { get; set; }
    public DbSet<VodMstr> VodMstr { get; set; }
    public DbSet<ApCtrl> ApCtrl { get; set; }

    // ── Accounts Receivable ───────────────────────────────────────────────────
    public DbSet<ArMstr> ArMstr { get; set; }
    public DbSet<ArdMstr> ArdMstr { get; set; }
    public DbSet<ArCtrl> ArCtrl { get; set; }

    // ── Inventory ─────────────────────────────────────────────────────────────
    public DbSet<ItemMstr> ItemMstr { get; set; }
    public DbSet<ItemCost> ItemCost { get; set; }
    public DbSet<BomMstr> BomMstr { get; set; }
    public DbSet<PbmMstr> PbmMstr { get; set; }
    public DbSet<WfMstr> WfMstr { get; set; }
    public DbSet<WcMstr> WcMstr { get; set; }
    public DbSet<LocMstr> LocMstr { get; set; }
    public DbSet<UomMstr> UomMstr { get; set; }
    public DbSet<WhMstr> WhMstr { get; set; }

    // ── Customer / Order Entry ────────────────────────────────────────────────
    public DbSet<CmMstr> CmMstr { get; set; }
    public DbSet<CmsDet> CmsDet { get; set; }
    public DbSet<CmcDet> CmcDet { get; set; }
    public DbSet<CupMstr> CupMstr { get; set; }
    public DbSet<CprMstr> CprMstr { get; set; }
    public DbSet<CustTerm> CustTerms { get; set; }
    public DbSet<SlspMstr> SlspMstr { get; set; }
    public DbSet<CmCtrl> CmCtrl { get; set; }
    public DbSet<SoMstr> SoMstr { get; set; }
    public DbSet<SodDet> SodDet { get; set; }
    public DbSet<SoTax> SoTax { get; set; }
    public DbSet<SodTax> SodTax { get; set; }
    public DbSet<SosDet> SosDet { get; set; }
    public DbSet<SvMstr> SvMstr { get; set; }
    public DbSet<SvdDet> SvdDet { get; set; }
    public DbSet<PosMstr> PosMstr { get; set; }
    public DbSet<PosDet> PosDet { get; set; }

    // ── Purchasing ────────────────────────────────────────────────────────────
    public DbSet<PoMstr> PoMstr { get; set; }
    public DbSet<PodMstr> PodMstr { get; set; }
    public DbSet<PoAddr> PoAddr { get; set; }
    public DbSet<PoMeta> PoMeta { get; set; }
    public DbSet<PoCtrl> PoCtrl { get; set; }
    public DbSet<PoTax> PoTax { get; set; }
    public DbSet<PodTax> PodTax { get; set; }

    // ── Shipping ──────────────────────────────────────────────────────────────
    public DbSet<ShipMstr> ShipMstr { get; set; }
    public DbSet<ShipDet> ShipDet { get; set; }
    public DbSet<ShsDet> ShsDet { get; set; }
    public DbSet<ShipCtrl> ShipCtrl { get; set; }
    public DbSet<ShipTree> ShipTree { get; set; }
    public DbSet<ShMeta> ShMeta { get; set; }

    // ── Vendor ────────────────────────────────────────────────────────────────
    public DbSet<VdMstr> VdMstr { get; set; }
    public DbSet<VdCtrl> VdCtrl { get; set; }
    public DbSet<VdpMstr> VdpMstr { get; set; }
    public DbSet<VprMstr> VprMstr { get; set; }
    public DbSet<VdsDet> VdsDet { get; set; }

    // ── Receiving ─────────────────────────────────────────────────────────────
    public DbSet<RecvMstr> RecvMstr { get; set; }
    public DbSet<RecvDet> RecvDet { get; set; }

    // ── HR / Payroll ──────────────────────────────────────────────────────────
    public DbSet<EmpMstr> EmpMstr { get; set; }
    public DbSet<EmpException> EmpExceptions { get; set; }

    // ── EDI ───────────────────────────────────────────────────────────────────
    public DbSet<EdiXref> EdiXref { get; set; }
    public DbSet<EdpPartner> EdpPartner { get; set; }
    public DbSet<EdiDoc> EdiDoc { get; set; }
    public DbSet<EdiDocdet> EdiDocdet { get; set; }
    public DbSet<EdiMstr> EdiMstr { get; set; }

    // ── Engineering ───────────────────────────────────────────────────────────
    public DbSet<EcnMstr> EcnMstr { get; set; }
    public DbSet<EcnTask> EcnTask { get; set; }
    public DbSet<TaskMstr> TaskMstr { get; set; }
    public DbSet<TaskDet> TaskDet { get; set; }

    // ── Freight ───────────────────────────────────────────────────────────────
    public DbSet<CarMstr> CarMstr { get; set; }
    public DbSet<CfoMstr> CfoMstr { get; set; }
    public DbSet<CfoDet> CfoDet { get; set; }
    public DbSet<CfoItem> CfoItem { get; set; }
    public DbSet<VehMstr> VehMstr { get; set; }
    public DbSet<DrvMstr> DrvMstr { get; set; }

    // ── Production / Scheduling ───────────────────────────────────────────────
    public DbSet<PlanMstr> PlanMstr { get; set; }
    public DbSet<PlanOperation> PlanOperations { get; set; }

    // ── Production ─────────────────────────────────────────────────────────────
    public DbSet<JobClock> JobClocks { get; set; }

    // ── Distribution Order ────────────────────────────────────────────────────
    public DbSet<DoMstr> DoMstr { get; set; }
    public DbSet<DodMstr> DodMstr { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Identity table rename (mirrors original user/role tables) ─────────
        modelBuilder.Entity<ZaffreMeldUser>().ToTable("zm_users");
        modelBuilder.Entity<ZaffreMeldRole>().ToTable("zm_roles");

        // ── Composite keys ────────────────────────────────────────────────────
        modelBuilder.Entity<ExcMstr>().HasKey(x => new { x.ExcBase, x.ExcForeign });
        modelBuilder.Entity<ItemCost>().HasKey(x => new { x.ItcItem, x.ItcSite, x.ItcSet });
        modelBuilder.Entity<PbmMstr>().HasKey(x => new { x.PsParent, x.PsChild });
        modelBuilder.Entity<SodDet>().HasKey(x => new { x.SodNbr, x.SodLine });
        modelBuilder.Entity<PodMstr>().HasKey(x => new { x.PodNbr, x.PodLine });
        modelBuilder.Entity<ShipDet>().HasKey(x => new { x.ShdId, x.ShdLine });
        modelBuilder.Entity<CmsDet>().HasKey(x => new { x.CmsCode, x.CmsShipto });
        modelBuilder.Entity<VdsDet>().HasKey(x => new { x.VdsCode, x.VdsShipto });
        modelBuilder.Entity<RecvDet>().HasKey(x => new { x.RvdId, x.RvdPoline });
        modelBuilder.Entity<TaxdMstr>().HasKey(x => new { x.TaxdParentcode, x.TaxdId });
        modelBuilder.Entity<EdiDocdet>().HasKey(x => new { x.EdidId, x.EdidRole, x.EdidRectype });
        modelBuilder.Entity<EcnTask>().HasKey(x => new { x.EcntNbr, x.EcntMstrid });
        modelBuilder.Entity<FtpAttr>().HasKey(x => new { x.FtpaId, x.FtpaKey });
        modelBuilder.Entity<CupMstr>().HasKey(x => new { x.CupCust, x.CupItem });
        modelBuilder.Entity<VdpMstr>().HasKey(x => new { x.VdpVend, x.VdpItem });

        // ── Indexes ───────────────────────────────────────────────────────────
        modelBuilder.Entity<GlTran>().HasIndex(x => x.GltEffdate);
        modelBuilder.Entity<GlTran>().HasIndex(x => x.GltAcct);
        modelBuilder.Entity<SoMstr>().HasIndex(x => x.SoCust);
        modelBuilder.Entity<PoMstr>().HasIndex(x => x.PoVend);
        modelBuilder.Entity<ItemMstr>().HasIndex(x => x.ItItem).IsUnique();
        modelBuilder.Entity<CmMstr>().HasIndex(x => x.CmCode).IsUnique();
        modelBuilder.Entity<VdMstr>().HasIndex(x => x.VdAddr).IsUnique();

        // ── Global convention: all non-nullable string columns default to "" ──
        // Prevents SQLite NOT NULL violations when MVC model binding produces
        // nulls for optional form fields the user left blank.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties()
                         .Where(p => p.ClrType == typeof(string) && !p.IsNullable))
            {
                property.SetDefaultValue(string.Empty);
            }
        }
    }
}
