using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZaffreMeld.Web.Controllers.Api;

/// <summary>
/// Finance API controller — replaces Java dataServFIN.java servlet.
/// Covers GL accounts, transactions, AP, AR, bank, currency, exchange rates.
/// All endpoints require authentication (mirrors Java confirmServerAuth check).
/// </summary>
[ApiController]
[Route("api/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _svc;
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(IFinanceService svc, Data.ZaffreMeldDbContext db, ILogger<FinanceController> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Chart of Accounts ──────────────────────────────────────────────────────

    /// <summary>GET /api/finance/accounts/{id} — getAcctMstr</summary>
    [HttpGet("accounts/{id}")]
    public async Task<IActionResult> GetAccount(string id)
    {
        var acct = await _svc.GetAccount(id);
        return acct == null ? NotFound() : Ok(acct);
    }

    /// <summary>GET /api/finance/accounts?from=&to= — getGLAcctListRangeWCurrTypeDesc</summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts([FromQuery] string from = "", [FromQuery] string to = "~")
    {
        var list = await _svc.GetAccountsInRange(from, to);
        return Ok(list);
    }

    /// <summary>POST /api/finance/accounts — addAcctMstr</summary>
    [HttpPost("accounts")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> AddAccount([FromBody] AcctMstr acct)
    {
        var result = await _svc.AddAccount(acct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/finance/accounts/{id} — updateAcctMstr</summary>
    [HttpPut("accounts/{id}")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> UpdateAccount(string id, [FromBody] AcctMstr acct)
    {
        if (id != acct.Id) return BadRequest("ID mismatch.");
        var result = await _svc.UpdateAccount(acct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/finance/accounts/{id} — deleteAcctMstr</summary>
    [HttpDelete("accounts/{id}")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> DeleteAccount(string id)
    {
        var result = await _svc.DeleteAccount(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── GL Transactions ────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/gltran?acct=&from=&to= — getGLTran</summary>
    [HttpGet("gltran")]
    public async Task<IActionResult> GetGlTran(
        [FromQuery] string acct,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var trans = await _svc.GetGlTransactions(acct, from, to);
        return Ok(trans);
    }

    /// <summary>POST /api/finance/gltran — addGLtran</summary>
    [HttpPost("gltran")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> PostGlTran([FromBody] GlTran tran)
    {
        var result = await _svc.PostGlTransaction(tran);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/finance/gltran/batch — addGLtrans</summary>
    [HttpPost("gltran/batch")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> PostGlTranBatch([FromBody] List<GlTran> trans)
    {
        var result = await _svc.PostGlTransactions(trans);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>POST /api/finance/glpair — addGLpair (balanced debit+credit)</summary>
    [HttpPost("glpair")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> PostGlPair([FromBody] GlPair pair)
    {
        var result = await _svc.PostGlPair(pair);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/finance/gltran/{id} — deleteGL</summary>
    [HttpDelete("gltran/{id}")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> DeleteGlTran(int id)
    {
        var tran = await _db.GlTran.FindAsync(id);
        if (tran == null) return NotFound();
        if (tran.GltPosted) return BadRequest("Cannot delete a posted transaction.");
        _db.GlTran.Remove(tran);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ── Bank / Currency ────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/banks/{id}</summary>
    [HttpGet("banks/{id}")]
    public async Task<IActionResult> GetBank(string id)
    {
        var bank = await _db.BankMstr.FindAsync(id);
        return bank == null ? NotFound() : Ok(bank);
    }

    /// <summary>GET /api/finance/banks</summary>
    [HttpGet("banks")]
    public IActionResult GetBanks()
        => Ok(_db.BankMstr.Where(b => b.CbActive).OrderBy(b => b.Id).ToList());

    /// <summary>POST /api/finance/banks</summary>
    [HttpPost("banks")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> AddBank([FromBody] BankMstr bank)
    {
        _db.BankMstr.Add(bank);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>PUT /api/finance/banks/{id}</summary>
    [HttpPut("banks/{id}")]
    [Authorize(Roles = "admin,finance")]
    public async Task<IActionResult> UpdateBank(string id, [FromBody] BankMstr bank)
    {
        _db.BankMstr.Update(bank);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>GET /api/finance/currencies</summary>
    [HttpGet("currencies")]
    public IActionResult GetCurrencies()
        => Ok(_db.CurrMstr.OrderBy(c => c.Id).ToList());

    /// <summary>GET /api/finance/currencies/{id}</summary>
    [HttpGet("currencies/{id}")]
    public async Task<IActionResult> GetCurrency(string id)
    {
        var curr = await _db.CurrMstr.FindAsync(id);
        return curr == null ? NotFound() : Ok(curr);
    }

    /// <summary>GET /api/finance/exchangerates/{base} — getExcMstr</summary>
    [HttpGet("exchangerates/{baseCurr}")]
    public IActionResult GetExchangeRates(string baseCurr)
    {
        var rates = _db.ExcMstr.Where(e => e.ExcBase == baseCurr).ToList();
        return Ok(rates);
    }

    // ── Departments ────────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/departments/{id} — getDeptMstr</summary>
    [HttpGet("departments/{id}")]
    public async Task<IActionResult> GetDepartment(string id)
    {
        var dept = await _db.DeptMstr.FindAsync(id);
        return dept == null ? NotFound() : Ok(dept);
    }

    /// <summary>GET /api/finance/departments</summary>
    [HttpGet("departments")]
    public IActionResult GetDepartments()
        => Ok(_db.DeptMstr.Where(d => d.DeptActive).OrderBy(d => d.DeptId).ToList());

    // ── Tax ────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/taxes — getTaxDet</summary>
    [HttpGet("taxes")]
    public IActionResult GetTaxes([FromQuery] string? code = null)
    {
        var query = _db.TaxMstr.AsQueryable();
        if (code != null) query = query.Where(t => t.TaxCode == code);
        return Ok(query.OrderBy(t => t.TaxCode).ToList());
    }

    /// <summary>GET /api/finance/taxes/{code}/details</summary>
    [HttpGet("taxes/{code}/details")]
    public IActionResult GetTaxDetails(string code)
        => Ok(_db.TaxdMstr.Where(t => t.TaxdParentcode == code).ToList());

    // ── AR ─────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/ar/{id} — getARMstr</summary>
    [HttpGet("ar/{id}")]
    public async Task<IActionResult> GetAr(string id)
    {
        var ar = await _db.ArMstr.FindAsync(id);
        return ar == null ? NotFound() : Ok(ar);
    }

    /// <summary>GET /api/finance/ar/{id}/lines</summary>
    [HttpGet("ar/{id}/lines")]
    public IActionResult GetArLines(string id)
    {
        var lines = _db.ArdMstr.Where(l => l.ArdNbr == id).OrderBy(l => l.ArdLine).ToList();
        return Ok(lines);
    }

    /// <summary>GET /api/finance/ar/aging — ARAgingView</summary>
    [HttpGet("ar/aging")]
    public IActionResult GetArAging([FromQuery] string site, [FromQuery] string? cust = null)
    {
        var query = _db.ArMstr.Where(a => a.ArSite == site && a.ArStatus == "O");
        if (cust != null) query = query.Where(a => a.ArCust == cust);
        return Ok(query.OrderBy(a => a.ArDuedate).ToList());
    }

    // ── AP ─────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/ap/{id}</summary>
    [HttpGet("ap/{id}")]
    public async Task<IActionResult> GetAp(string id)
    {
        var ap = await _db.ApMstr.FindAsync(id);
        return ap == null ? NotFound() : Ok(ap);
    }

    /// <summary>GET /api/finance/ap/aging — APAgingView</summary>
    [HttpGet("ap/aging")]
    public IActionResult GetApAging([FromQuery] string site, [FromQuery] string? vend = null)
    {
        var query = _db.ApMstr.Where(a => a.ApSite == site && a.ApStatus == "O");
        if (vend != null) query = query.Where(a => a.ApVend == vend);
        return Ok(query.OrderBy(a => a.ApDuedate).ToList());
    }

    // ── GL Control ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/finance/control — getGLCtrl / getFINInit</summary>
    [HttpGet("control")]
    public IActionResult GetControl([FromQuery] string site = "DEFAULT")
    {
        var ctrl = _db.GlCtrl.FirstOrDefault(c => c.GlSite == site);
        return ctrl == null ? NotFound() : Ok(ctrl);
    }

    /// <summary>GET /api/finance/account-balance?acct=&cc=&year=&period=</summary>
    [HttpGet("account-balance")]
    public async Task<IActionResult> GetAccountBalance(
        [FromQuery] string acct,
        [FromQuery] string cc = "",
        [FromQuery] string year = "",
        [FromQuery] string period = "")
    {
        var balance = await _svc.GetAccountBalance(acct, cc, year, period);
        return Ok(new { account = acct, cc, year, period, balance });
    }
}
