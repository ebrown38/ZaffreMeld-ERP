using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Models.HR;
using ZaffreMeld.Web.Models.Scheduling;
using ZaffreMeld.Web.Models.Production;
using ZaffreMeld.Web.Models.Receiving;
using ZaffreMeld.Web.Models.Distribution;
using ZaffreMeld.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZaffreMeld.Web.Controllers.Api;

// ─────────────────────────────────────────────────────────────────────────────
// ADMINISTRATION
// Replaces: dataServADM.java, dataServOV.java
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdministrationController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<AdministrationController> _logger;

    public AdministrationController(Data.ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<AdministrationController> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    // ── Sites ─────────────────────────────────────────────────────────────────

    [HttpGet("sites")]
    public IActionResult GetSites()
        => Ok(_db.Sites.OrderBy(s => s.SiteSite).ToList());

    [HttpGet("sites/{id}")]
    public IActionResult GetSite(string id)
    {
        var site = _db.Sites.FirstOrDefault(s => s.SiteSite == id);
        return site == null ? NotFound() : Ok(site);
    }

    [HttpPost("sites")]
    public async Task<IActionResult> AddSite([FromBody] SiteMstr site)
    {
        _db.Sites.Add(site);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPut("sites/{id}")]
    public async Task<IActionResult> UpdateSite(string id, [FromBody] SiteMstr site)
    {
        var existing = _db.Sites.FirstOrDefault(s => s.SiteSite == id);
        if (existing == null) return NotFound();
        existing.SiteDesc     = site.SiteDesc;
        existing.SiteLine1    = site.SiteLine1;
        existing.SiteLine2    = site.SiteLine2;
        existing.SiteCity     = site.SiteCity;
        existing.SiteState    = site.SiteState;
        existing.SiteZip      = site.SiteZip;
        existing.SiteCountry  = site.SiteCountry;
        existing.SitePhone    = site.SitePhone;
        existing.SiteFax      = site.SiteFax;
        existing.SiteCurrency = site.SiteCurrency;
        existing.SiteActive   = site.SiteActive;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ── Code Master (Lookup tables) ────────────────────────────────────────────

    [HttpGet("codes")]
    [AllowAnonymous]
    public IActionResult GetCodes([FromQuery] string? code = null)
    {
        var q = _db.CodeMstr.AsQueryable();
        if (code != null) q = q.Where(c => c.CodeCode == code);
        return Ok(q.OrderBy(c => c.CodeCode).ThenBy(c => c.CodeKey).ToList());
    }

    [HttpGet("codes/{code}/{key}")]
    [AllowAnonymous]
    public IActionResult GetCode(string code, string key)
    {
        var cm = _db.CodeMstr.FirstOrDefault(c => c.CodeCode == code && c.CodeKey == key);
        return cm == null ? NotFound() : Ok(cm);
    }

    [HttpPost("codes")]
    public async Task<IActionResult> AddCode([FromBody] CodeMstr cm)
    {
        _db.CodeMstr.Add(cm);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpDelete("codes/{code}/{key}")]
    public async Task<IActionResult> DeleteCode(string code, string key)
    {
        var cm = _db.CodeMstr.FirstOrDefault(c => c.CodeCode == code && c.CodeKey == key);
        if (cm == null) return NotFound();
        _db.CodeMstr.Remove(cm);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ── Menu Master ────────────────────────────────────────────────────────────

    [HttpGet("menu")]
    [AllowAnonymous]
    public IActionResult GetMenu([FromQuery] string? role = null)
    {
        var q = _db.MenuMstr.Where(m => m.MenuActive == "1");
        if (role != null) q = q.Where(m => m.MenuRole == role || m.MenuRole == "");
        return Ok(q.OrderBy(m => m.MenuParent).ThenBy(m => m.MenuSeq).ToList());
    }

    // ── Counters ───────────────────────────────────────────────────────────────

    [HttpGet("counters")]
    public IActionResult GetCounters()
        => Ok(_db.Counters.OrderBy(c => c.CounterName).ToList());

    [HttpPost("counters/next/{name}")]
    public async Task<IActionResult> GetNextNumber(string name)
    {
        var next = await _app.GetNextDocumentNumber(name);
        return Ok(new { counter = name, next });
    }

    // ── Users (admin view) ─────────────────────────────────────────────────────

    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        var users = _db.ZaffreMeldUsers
            .Select(u => new { u.UserId, u.UserSite, u.FirstName, u.LastName, u.UserType, u.IsActive, u.LastLogin })
            .OrderBy(u => u.UserId)
            .ToList();
        return Ok(users);
    }

    // ── Audit Log ─────────────────────────────────────────────────────────────

    [HttpGet("changelog")]
    public IActionResult GetChangeLog(
        [FromQuery] string? table = null,
        [FromQuery] string? user = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var q = _db.ChangeLogs.AsQueryable();
        if (table != null) q = q.Where(c => c.ClTable == table);
        if (user != null) q = q.Where(c => c.ClUser == user);
        var total = q.Count();
        var logs = q.OrderByDescending(c => c.ClTimestamp).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { total, page, pageSize, logs });
    }

    // ── OV Control ────────────────────────────────────────────────────────────

    [HttpGet("control")]
    public IActionResult GetOvControl()
    {
        var ctrl = _db.OvCtrl.FirstOrDefault();
        return ctrl == null ? NotFound() : Ok(ctrl);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HR
// Replaces: dataServHRM.java, ClockMaint, ClockControl etc.
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/hr")]
[Authorize]
public class HrController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly ILogger<HrController> _logger;

    public HrController(Data.ZaffreMeldDbContext db, ILogger<HrController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("employees/{id}")]
    public async Task<IActionResult> GetEmployee(string id)
    {
        var emp = await _db.EmpMstr.FindAsync(id);
        return emp == null ? NotFound() : Ok(emp);
    }

    [HttpGet("employees")]
    public IActionResult GetEmployees(
        [FromQuery] string? dept = null,
        [FromQuery] string? site = null,
        [FromQuery] string? status = "A")
    {
        var q = _db.EmpMstr.AsQueryable();
        if (dept != null) q = q.Where(e => e.EmpDept == dept);
        if (site != null) q = q.Where(e => e.EmpSite == site);
        if (status != null) q = q.Where(e => e.EmpStatus == status);
        return Ok(q.OrderBy(e => e.EmpLname).ThenBy(e => e.EmpFname).ToList());
    }

    [HttpPost("employees")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> AddEmployee([FromBody] EmpMstr emp)
    {
        if (_db.EmpMstr.Any(e => e.EmpNbr == emp.EmpNbr))
            return BadRequest(new { success = false, message = "Employee number already exists." });
        emp.EmpUser = User.Identity?.Name ?? string.Empty;
        _db.EmpMstr.Add(emp);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, empNbr = emp.EmpNbr });
    }

    [HttpPut("employees/{id}")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> UpdateEmployee(string id, [FromBody] EmpMstr emp)
    {
        if (id != emp.EmpNbr) return BadRequest("ID mismatch.");
        _db.EmpMstr.Update(emp);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("employees/{id}/time")]
    public IActionResult GetEmployeeTime(string id, [FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        var q = _db.EmpExceptions.Where(e => e.EmpxNbr == id);
        if (from != null) q = q.Where(e => string.Compare(e.EmpxDate, from) >= 0);
        if (to != null) q = q.Where(e => string.Compare(e.EmpxDate, to) <= 0);
        return Ok(q.OrderByDescending(e => e.EmpxDate).ToList());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCTION / WORK ORDERS
// Replaces: dataServPRD.java, WorkOrdServ.java, ClockMaint
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/production")]
[Authorize]
public class ProductionController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<ProductionController> _logger;

    public ProductionController(Data.ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<ProductionController> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    [HttpGet("workorders/{id}")]
    public IActionResult GetWorkOrder(int id)
    {
        var wo = _db.PlanMstr.Find(id);
        if (wo == null) return NotFound();
        var ops = _db.PlanOperations.Where(o => o.PloParent == id).OrderBy(o => o.PloSeq).ToList();
        return Ok(new { header = wo, operations = ops });
    }

    [HttpGet("workorders")]
    public IActionResult GetWorkOrders(
        [FromQuery] string? item = null,
        [FromQuery] string? status = null,
        [FromQuery] string? site = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var q = _db.PlanMstr.AsQueryable();
        if (item != null) q = q.Where(w => w.PlanItem == item);
        if (status != null) q = q.Where(w => w.PlanStatus == status);
        if (site != null) q = q.Where(w => w.PlanSite == site);
        var total = q.Count();
        var orders = q.OrderByDescending(w => w.PlanDuedate).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { total, page, pageSize, orders });
    }

    [HttpPost("workorders")]
    [Authorize(Roles = "admin,production")]
    public async Task<IActionResult> CreateWorkOrder([FromBody] PlanMstr wo)
    {
        wo.PlanUser = User.Identity?.Name ?? string.Empty;
        wo.PlanStatus = "O";
        _db.PlanMstr.Add(wo);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, woId = wo.PlanNbr });
    }

    [HttpPost("workorders/{id}/close")]
    [Authorize(Roles = "admin,production")]
    public async Task<IActionResult> CloseWorkOrder(int id)
    {
        var wo = await _db.PlanMstr.FindAsync(id);
        if (wo == null) return NotFound();
        wo.PlanStatus = "C";
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ── Clock / Labor Tracking ────────────────────────────────────────────────

    [HttpPost("clock/in")]
    public async Task<IActionResult> ClockIn([FromBody] ClockInRequest req)
    {
        var jc = new JobClock
        {
            JobcPlanid = req.WorkOrderId,
            JobcOp = req.OperationId,
            JobcEmpnbr = req.EmpNbr,
            JobcDate = DateTime.Today.ToString("yyyy-MM-dd"),
            JobcTimein = DateTime.Now.ToString("HH:mm:ss"),
            JobcSite = req.Site ?? _app.GetSite(),
            JobcPosted = false
        };
        _db.JobClocks.Add(jc);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, clockId = jc.Id });
    }

    [HttpPost("clock/{clockId}/out")]
    public async Task<IActionResult> ClockOut(int clockId, [FromBody] ClockOutRequest req)
    {
        var jc = await _db.JobClocks.FindAsync(clockId);
        if (jc == null) return NotFound();

        jc.JobcTimeout = DateTime.Now.ToString("HH:mm:ss");
        jc.JobcQty = req.Qty;

        // Calculate hours
        if (TimeSpan.TryParse(jc.JobcTimein, out var inTime) && TimeSpan.TryParse(jc.JobcTimeout, out var outTime))
            jc.JobcHours = (decimal)(outTime - inTime).TotalHours;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, hours = jc.JobcHours });
    }
}

public record ClockInRequest(int WorkOrderId, int OperationId, string EmpNbr, string? Site);
public record ClockOutRequest(decimal Qty);

// ─────────────────────────────────────────────────────────────────────────────
// RECEIVING
// Replaces: dataServRCV.java
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/receiving")]
[Authorize]
public class ReceivingController : ControllerBase
{
    private readonly Data.ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<ReceivingController> _logger;

    public ReceivingController(Data.ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<ReceivingController> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public IActionResult GetReceiver(string id)
    {
        var rv = _db.RecvMstr.Find(id);
        if (rv == null) return NotFound();
        var lines = _db.RecvDet.Where(l => l.RvdId == id).ToList();
        return Ok(new { header = rv, lines });
    }

    [HttpGet]
    public IActionResult GetReceivers(
        [FromQuery] string? vend = null,
        [FromQuery] string? status = null,
        [FromQuery] string? site = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var q = _db.RecvMstr.AsQueryable();
        if (vend != null) q = q.Where(r => r.RvVend == vend);
        if (status != null) q = q.Where(r => r.RvStatus == status);
        if (site != null) q = q.Where(r => r.RvSite == site);
        var total = q.Count();
        var recs = q.OrderByDescending(r => r.RvRecvdate).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { total, page, pageSize, recs });
    }

    [HttpPost]
    [Authorize(Roles = "admin,purchasing")]
    public async Task<IActionResult> CreateReceiver([FromBody] CreateReceiverRequest req)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrEmpty(req.Header.RvId))
                req.Header.RvId = await _app.GetNextDocumentNumber("RV");

            req.Header.RvRecvdate = DateTime.Today.ToString("yyyy-MM-dd");
            req.Header.RvStatus = "O";
            req.Header.RvUser = User.Identity?.Name ?? string.Empty;

            foreach (var line in req.Lines)
                line.RvdId = req.Header.RvId;

            _db.RecvMstr.Add(req.Header);
            _db.RecvDet.AddRange(req.Lines);

            // Update PO received quantities
            foreach (var line in req.Lines)
            {
                var podLine = _db.PodMstr.FirstOrDefault(p => p.PodNbr == line.RvdPo && p.PodLine == line.RvdPoline);
                if (podLine != null)
                {
                    podLine.PodQtyrcv += line.RvdQty;
                    if (podLine.PodQtyrcv >= podLine.PodQty)
                        podLine.PodStatus = "C";
                }

                // Update item QOH
                var item = _db.ItemMstr.FirstOrDefault(i => i.ItItem == line.RvdItem && i.ItSite == req.Header.RvSite);
                if (item != null) item.ItQoh += line.RvdQty;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { success = true, receiverId = req.Header.RvId });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error creating receiver");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public record CreateReceiverRequest(Models.Receiving.RecvMstr Header, List<Models.Receiving.RecvDet> Lines);