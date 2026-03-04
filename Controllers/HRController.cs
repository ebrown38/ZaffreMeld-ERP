using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZaffreMeld.Web.Controllers;

[Authorize]
[Route("hr")]
public class HRController : Controller
{
    private readonly ZaffreMeldDbContext _db;
    public HRController(ZaffreMeldDbContext db) => _db = db;

    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] string? q, [FromQuery] string? status)
    {
        var query = _db.EmpMstr.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(e => e.EmpStatus == status);
        if (!string.IsNullOrEmpty(q))      query = query.Where(e => e.EmpNbr.Contains(q) || e.EmpLname.Contains(q) || e.EmpFname.Contains(q));
        ViewBag.Employees = await query.OrderBy(e => e.EmpLname).ThenBy(e => e.EmpFname).Take(200).ToListAsync();
        ViewBag.Q = q;
        return View();
    }

    [HttpGet("employees/{id}")]
    public async Task<IActionResult> Employee(string id)
    {
        var emp = await _db.EmpMstr.FindAsync(id);
        if (emp == null) return NotFound();
        ViewBag.Exceptions = await _db.EmpExceptions.Where(e => e.EmpxNbr == id)
            .OrderByDescending(e => e.EmpxDate).Take(50).ToListAsync();
        return View(emp);
    }

    [HttpGet("employees/new")]
    public IActionResult NewEmployee() => View(new EmpMstr { EmpStatus = "A", EmpType = "H", EmpShift = "1" });

    [HttpPost("employees/new")]
    public async Task<IActionResult> NewEmployee([FromForm] EmpMstr emp)
    {
        emp.EmpHiredate = DateTime.Today.ToString("yyyy-MM-dd");
        emp.EmpUser     = User.Identity?.Name ?? string.Empty;
        emp.EmpNbr      ??= string.Empty;
        emp.EmpLname    ??= string.Empty;
        emp.EmpFname    ??= string.Empty;
        emp.EmpMiddle   ??= string.Empty;
        emp.EmpDept     ??= string.Empty;
        emp.EmpType     ??= "H";
        emp.EmpStatus   ??= "A";
        emp.EmpSite     ??= string.Empty;
        emp.EmpWcell    ??= string.Empty;
        emp.EmpNote     ??= string.Empty;
        emp.EmpShift    ??= "1";
        emp.EmpTermdate ??= string.Empty;
        _db.EmpMstr.Add(emp);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Employee), new { id = emp.EmpNbr });
    }

    [HttpGet("training")]
    public async Task<IActionResult> Training()
    {
        ViewBag.Employees = await _db.EmpMstr.Where(e => e.EmpStatus == "A")
            .OrderBy(e => e.EmpLname).ToListAsync();
        return View();
    }
}
