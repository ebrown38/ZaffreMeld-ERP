using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("finance")]
public class FinanceController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public FinanceController(ZaffreMeldDbContext db) => _db = db;

    // ── Chart of Accounts ──────────────────────────────────────────────────────

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts([FromQuery] string? q, [FromQuery] string? type)
    {
        var query = _db.AcctMstr.AsQueryable();
        if (!string.IsNullOrEmpty(type)) query = query.Where(a => a.Type == type);
        if (!string.IsNullOrEmpty(q))    query = query.Where(a => a.Id.Contains(q) || a.Desc.Contains(q));
        ViewBag.Accounts = await query.OrderBy(a => a.Id).Take(300).ToListAsync();
        ViewBag.Q = q; ViewBag.Type = type;
        return View();
    }

    [HttpGet("accounts/{id}")]
    public async Task<IActionResult> Account(string id,
        [FromQuery] string? from, [FromQuery] string? to)
    {
        var acct = await _db.AcctMstr.FindAsync(id);
        if (acct == null) return NotFound();
        var txQuery = _db.GlTran.Where(t => t.GltAcct == id);
        if (!string.IsNullOrEmpty(from)) txQuery = txQuery.Where(t => string.Compare(t.GltEffdate, from) >= 0);
        if (!string.IsNullOrEmpty(to))   txQuery = txQuery.Where(t => string.Compare(t.GltEffdate, to) <= 0);
        ViewBag.Transactions = await txQuery.OrderByDescending(t => t.GltEffdate).Take(100).ToListAsync();
        ViewBag.Balance = ((List<GlTran>)ViewBag.Transactions).Sum(t => t.GltAmt);
        ViewBag.From = from; ViewBag.To = to;
        return View(acct);
    }

    // ── GL Transactions ────────────────────────────────────────────────────────

    [HttpGet("gltran")]
    public async Task<IActionResult> GlTransactions(
        [FromQuery] string? acct, [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] string? source)
    {
        var query = _db.GlTran.AsQueryable();
        if (!string.IsNullOrEmpty(acct))   query = query.Where(t => t.GltAcct == acct);
        if (!string.IsNullOrEmpty(source)) query = query.Where(t => t.GltType == source);
        if (!string.IsNullOrEmpty(from))   query = query.Where(t => string.Compare(t.GltEffdate, from) >= 0);
        if (!string.IsNullOrEmpty(to))     query = query.Where(t => string.Compare(t.GltEffdate, to) <= 0);
        ViewBag.Transactions = await query.OrderByDescending(t => t.GltEffdate).Take(300).ToListAsync();
        ViewBag.Accounts = await _db.AcctMstr.OrderBy(a => a.Id).ToListAsync();
        ViewBag.Acct = acct; ViewBag.From = from; ViewBag.To = to;
        return View();
    }

    // ── AR ─────────────────────────────────────────────────────────────────────

    [HttpGet("ar")]
    public async Task<IActionResult> AR([FromQuery] string? status, [FromQuery] string? q)
    {
        var query = _db.ArMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.ArStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(a => a.ArId.Contains(q) || a.ArCust.Contains(q));
        ViewBag.Invoices = await query.OrderByDescending(a => a.ArId).Take(200).ToListAsync();
        ViewBag.Status = status; ViewBag.Q = q;
        return View();
    }

    [HttpGet("ar/{id}")]
    public async Task<IActionResult> ARInvoice(string id)
    {
        var inv = await _db.ArMstr.FindAsync(id);
        if (inv == null) return NotFound();
        ViewBag.Lines = await _db.ArdMstr.Where(l => l.ArdId == id).ToListAsync();
        ViewBag.Customer = await _db.CmMstr.FindAsync(inv.ArCust);
        return View(inv);
    }

    // ── AP ─────────────────────────────────────────────────────────────────────

    [HttpGet("ap")]
    public async Task<IActionResult> AP([FromQuery] string? status, [FromQuery] string? q)
    {
        var query = _db.ApMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.ApStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(a => a.ApId.Contains(q) || a.ApVend.Contains(q));
        ViewBag.Vouchers = await query.OrderByDescending(a => a.ApId).Take(200).ToListAsync();
        ViewBag.Status = status; ViewBag.Q = q;
        return View();
    }

    [HttpGet("ap/{id}")]
    public async Task<IActionResult> APVoucher(string id)
    {
        var vouch = await _db.ApMstr.FindAsync(id);
        if (vouch == null) return NotFound();
        ViewBag.Lines = await _db.ApdMstr.Where(l => l.ApdId == id).ToListAsync();
        ViewBag.Vendor = await _db.VdMstr.FindAsync(vouch.ApVend);
        return View(vouch);
    }

    // ── Trial Balance ──────────────────────────────────────────────────────────

    [HttpGet("trialbalance")]
    public async Task<IActionResult> TrialBalance([FromQuery] string? year, [FromQuery] string? period)
    {
        year ??= DateTime.Today.Year.ToString();
        period ??= DateTime.Today.Month.ToString("D2");
        var rows = await _db.GlTran
            .Where(t => t.GltYear == year && t.GltPeriod == period)
            .GroupBy(t => t.GltAcct)
            .Select(g => new { Account = g.Key, Debits = g.Sum(t => t.GltAmt > 0 ? t.GltAmt : 0), Credits = g.Sum(t => t.GltAmt < 0 ? -t.GltAmt : 0) })
            .OrderBy(r => r.Account)
            .ToListAsync();
        ViewBag.Rows = rows;
        ViewBag.Year = year; ViewBag.Period = period;
        ViewBag.TotalDebits = rows.Sum(r => r.Debits);
        ViewBag.TotalCredits = rows.Sum(r => r.Credits);
        return View();
    }

    // ── Balance Sheet ──────────────────────────────────────────────────────────

    [HttpGet("balancesheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] string? year, [FromQuery] string? period)
    {
        year ??= DateTime.Today.Year.ToString();
        period ??= DateTime.Today.Month.ToString("D2");
        var ctrl = await _db.GlCtrl.FindAsync(1);
        var accounts = await _db.AcctMstr.ToListAsync();
        var balances = await _db.GlTran
            .Where(t => t.GltYear == year && string.Compare(t.GltPeriod, period) <= 0)
            .GroupBy(t => t.GltAcct)
            .Select(g => new { Account = g.Key, Net = g.Sum(t => t.GltAmt) })
            .ToListAsync();
        ViewBag.Accounts = accounts;
        ViewBag.Balances = balances.ToDictionary(b => b.Account, b => b.Net);
        ViewBag.Ctrl = ctrl;
        ViewBag.Year = year; ViewBag.Period = period;
        return View();
    }
}
