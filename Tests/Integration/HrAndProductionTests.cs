using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.HR;
using ZaffreMeld.Web.Models.Production;
using ZaffreMeld.Web.Models.Scheduling;
using ZaffreMeld.Web.Services;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

// ── HR Controller ──────────────────────────────────────────────────────────────

public class HrControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly HrController _ctrl;

    public HrControllerTests()
    {
        _db  = TestDbFactory.Create();
        _ctrl = new HrController(_db, NullLogger<HrController>.Instance);
        SetUser(_ctrl, "hruser");

        _db.EmpMstr.AddRange(
            new EmpMstr { EmpNbr = "EMP-001", EmpFname = "Alice", EmpLname = "Anderson", EmpDept = "PROD", EmpSite = "DEFAULT", EmpStatus = "A", EmpType = "H", EmpRate = 22.50m, EmpHiredate = "2022-01-10" },
            new EmpMstr { EmpNbr = "EMP-002", EmpFname = "Bob",   EmpLname = "Baker",    EmpDept = "SHIPPING", EmpSite = "DEFAULT", EmpStatus = "A", EmpType = "S", EmpRate = 55000m, EmpHiredate = "2021-06-01" },
            new EmpMstr { EmpNbr = "EMP-003", EmpFname = "Carol", EmpLname = "Clark",    EmpDept = "PROD", EmpSite = "DEFAULT", EmpStatus = "I", EmpType = "H", EmpRate = 18.00m, EmpHiredate = "2019-03-15" }
        );
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetEmployee ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployee_ExistingId_Returns200()
    {
        var result = await _ctrl.GetEmployee("EMP-001") as OkObjectResult;
        result.Should().NotBeNull();
        ((EmpMstr)result.Value!).EmpFname.Should().Be("Alice");
    }

    [Fact]
    public async Task GetEmployee_NonExistent_Returns404()
    {
        (await _ctrl.GetEmployee("GHOST")).Should().BeOfType<NotFoundResult>();
    }

    // ── GetEmployees (browse) ──────────────────────────────────────────────────

    [Fact]
    public void GetEmployees_DefaultsToActiveOnly()
    {
        var result = _ctrl.GetEmployees(status: "A") as OkObjectResult;
        var list   = ((List<EmpMstr>)result.Value!);
        list.Should().HaveCount(2);
        list.Should().NotContain(e => e.EmpNbr == "EMP-003");
    }

    [Fact]
    public void GetEmployees_FiltersByDept()
    {
        var result = _ctrl.GetEmployees(dept: "PROD", status: null) as OkObjectResult;
        var list   = ((List<EmpMstr>)result.Value!);
        list.Should().HaveCount(2); // both active and inactive in PROD
        list.Should().OnlyContain(e => e.EmpDept == "PROD");
    }

    [Fact]
    public void GetEmployees_FiltersBySite()
    {
        var result = _ctrl.GetEmployees(site: "DEFAULT", status: "A") as OkObjectResult;
        ((List<EmpMstr>)result.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetEmployees_FiltersByStatusInactive()
    {
        var result = _ctrl.GetEmployees(status: "I") as OkObjectResult;
        var list   = ((List<EmpMstr>)result.Value!);
        list.Should().HaveCount(1);
        list.Single().EmpNbr.Should().Be("EMP-003");
    }

    [Fact]
    public void GetEmployees_OrdersByLastNameThenFirstName()
    {
        var result = _ctrl.GetEmployees(status: "A") as OkObjectResult;
        var list   = ((List<EmpMstr>)result.Value!);
        list[0].EmpLname.Should().Be("Anderson");
        list[1].EmpLname.Should().Be("Baker");
    }

    // ── AddEmployee ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddEmployee_NewEmployee_Returns200AndPersists()
    {
        var emp = new EmpMstr { EmpNbr = "EMP-NEW", EmpFname = "Dave", EmpLname = "Davis", EmpDept = "IT", EmpSite = "DEFAULT", EmpStatus = "A", EmpType = "S", EmpRate = 70000m };

        var result = await _ctrl.AddEmployee(emp);

        result.Should().BeOfType<OkObjectResult>();
        _db.EmpMstr.Find("EMP-NEW").Should().NotBeNull();
    }

    [Fact]
    public async Task AddEmployee_DuplicateNumber_Returns400()
    {
        var dup = new EmpMstr { EmpNbr = "EMP-001", EmpFname = "Dup", EmpLname = "Dup", EmpDept = "IT", EmpSite = "DEFAULT", EmpStatus = "A", EmpType = "H", EmpRate = 0m };

        var result = await _ctrl.AddEmployee(dup);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateEmployee ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEmployee_ChangesRate()
    {
        var emp = _db.EmpMstr.Find("EMP-001")!;
        emp.EmpRate = 25.00m;

        var result = await _ctrl.UpdateEmployee("EMP-001", emp);

        result.Should().BeOfType<OkObjectResult>();
        _db.EmpMstr.Find("EMP-001")!.EmpRate.Should().Be(25.00m);
    }

    [Fact]
    public async Task UpdateEmployee_IdMismatch_Returns400()
    {
        var emp = new EmpMstr { EmpNbr = "WRONG" };
        var result = await _ctrl.UpdateEmployee("EMP-001", emp);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetEmployeeTime ────────────────────────────────────────────────────────

    [Fact]
    public void GetEmployeeTime_WithExceptions_ReturnsThem()
    {
        _db.EmpExceptions.AddRange(
            new EmpException { EmpxNbr = "EMP-001", EmpxDesc = "Vacation",   EmpxType = "V", EmpxHours = 8m, EmpxDate = "2026-03-01", EmpxApproved = true },
            new EmpException { EmpxNbr = "EMP-001", EmpxDesc = "Sick Leave", EmpxType = "S", EmpxHours = 8m, EmpxDate = "2026-03-03", EmpxApproved = true }
        );
        _db.SaveChanges();

        var result = _ctrl.GetEmployeeTime("EMP-001") as OkObjectResult;
        ((List<EmpException>)result.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetEmployeeTime_FiltersByDateRange()
    {
        _db.EmpExceptions.AddRange(
            new EmpException { EmpxNbr = "EMP-001", EmpxDesc = "Old",     EmpxType = "V", EmpxHours = 8m, EmpxDate = "2025-01-01", EmpxApproved = true },
            new EmpException { EmpxNbr = "EMP-001", EmpxDesc = "Current", EmpxType = "V", EmpxHours = 8m, EmpxDate = "2026-03-01", EmpxApproved = true }
        );
        _db.SaveChanges();

        var result = _ctrl.GetEmployeeTime("EMP-001", from: "2026-01-01") as OkObjectResult;
        ((List<EmpException>)result.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetEmployeeTime_NoExceptions_ReturnsEmpty()
    {
        var result = _ctrl.GetEmployeeTime("EMP-002") as OkObjectResult;
        ((List<EmpException>)result.Value!).Should().BeEmpty();
    }

    private static void SetUser(ControllerBase ctrl, string username)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, username) }, "test"))
            }
        };
    }
}

// ── Production Controller ──────────────────────────────────────────────────────

public class ProductionControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly ProductionController _ctrl;

    public ProductionControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetSite()).Returns("DEFAULT");

        _ctrl = new ProductionController(_db, _appMock.Object, NullLogger<ProductionController>.Instance);
        SetUser(_ctrl, "produser");

        // Seed a work order
        _db.PlanMstr.Add(new PlanMstr
        {
            PlanNbr       = 1001,
            PlanItem      = "WIDGET-100",
            PlanSite      = "DEFAULT",
            PlanStatus    = "O",
            PlanType      = "W",
            PlanQty       = 50m,
            PlanStartdate = "2026-03-01",
            PlanDuedate   = "2026-03-10"
        });
        _db.PlanOperations.AddRange(
            new PlanOperation { PloId = 1, PloParent = 1001, PloSeq = 10, PloCellid = "CELL-A", PloDesc = "Cut",  PloStatus = "O" },
            new PlanOperation { PloId = 2, PloParent = 1001, PloSeq = 20, PloCellid = "CELL-B", PloDesc = "Weld", PloStatus = "O" }
        );
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetWorkOrder ───────────────────────────────────────────────────────────

    [Fact]
    public void GetWorkOrder_ExistingId_Returns200WithOps()
    {
        var result = _ctrl.GetWorkOrder(1001) as OkObjectResult;
        result.Should().NotBeNull();
        var body = result!.Value;
        var ops  = Anon.Prop<List<PlanOperation>>(body, "operations");
        ops.Should().HaveCount(2);
    }

    [Fact]
    public void GetWorkOrder_NonExistent_Returns404()
    {
        _ctrl.GetWorkOrder(9999).Should().BeOfType<NotFoundResult>();
    }

    // ── GetWorkOrders (browse) ─────────────────────────────────────────────────

    [Fact]
    public void GetWorkOrders_FiltersByItem()
    {
        var result = _ctrl.GetWorkOrders(item: "WIDGET-100") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(1);
    }

    [Fact]
    public void GetWorkOrders_FiltersByStatus()
    {
        var result = _ctrl.GetWorkOrders(status: "O") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(1);
    }

    [Fact]
    public void GetWorkOrders_FiltersByStatus_Closed_ReturnsEmpty()
    {
        var result = _ctrl.GetWorkOrders(status: "C") as OkObjectResult;
        var body   = result!.Value;
        Anon.Prop<int>(body, "total").Should().Be(0);
    }

    // ── CreateWorkOrder ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateWorkOrder_Persists_AndSetsStatusOpen()
    {
        var wo = new PlanMstr
        {
            PlanNbr   = 2001,
            PlanItem  = "GADGET-200",
            PlanSite  = "DEFAULT",
            PlanQty   = 25m,
            PlanDuedate = "2026-04-01"
        };

        var result = await _ctrl.CreateWorkOrder(wo);

        result.Should().BeOfType<OkObjectResult>();
        _db.PlanMstr.Find(2001)!.PlanStatus.Should().Be("O");
    }

    [Fact]
    public async Task CreateWorkOrder_SetsUser()
    {
        var wo = new PlanMstr { PlanNbr = 2002, PlanItem = "GADGET-200", PlanSite = "DEFAULT", PlanQty = 10m, PlanDuedate = "2026-04-01" };
        await _ctrl.CreateWorkOrder(wo);

        _db.PlanMstr.Find(2002)!.PlanUser.Should().Be("produser");
    }

    // ── CloseWorkOrder ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseWorkOrder_ChangesStatusToClosed()
    {
        var result = await _ctrl.CloseWorkOrder(1001);

        result.Should().BeOfType<OkObjectResult>();
        _db.PlanMstr.Find(1001)!.PlanStatus.Should().Be("C");
    }

    [Fact]
    public async Task CloseWorkOrder_NonExistent_Returns404()
    {
        (await _ctrl.CloseWorkOrder(9999)).Should().BeOfType<NotFoundResult>();
    }

    // ── ClockIn ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockIn_CreatesJobClockRecord()
    {
        var req    = new ClockInRequest(WorkOrderId: 1001, OperationId: 1, EmpNbr: "EMP-001", Site: "DEFAULT");
        var result = await _ctrl.ClockIn(req);

        result.Should().BeOfType<OkObjectResult>();
        _db.JobClocks.Should().Contain(jc => jc.JobcPlanid == 1001 && jc.JobcEmpnbr == "EMP-001");
    }

    [Fact]
    public async Task ClockIn_SetsDateToToday()
    {
        var req = new ClockInRequest(1001, 1, "EMP-001", "DEFAULT");
        await _ctrl.ClockIn(req);

        var jc = _db.JobClocks.First();
        jc.JobcDate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task ClockIn_SetsPostedFalse()
    {
        var req = new ClockInRequest(1001, 1, "EMP-001", "DEFAULT");
        await _ctrl.ClockIn(req);

        _db.JobClocks.First().JobcPosted.Should().BeFalse();
    }

    [Fact]
    public async Task ClockIn_UsesSiteFromApp_WhenSiteNull()
    {
        var req = new ClockInRequest(1001, 1, "EMP-001", null);
        await _ctrl.ClockIn(req);

        _db.JobClocks.First().JobcSite.Should().Be("DEFAULT");
        _appMock.Verify(a => a.GetSite(), Times.Once);
    }

    // ── ClockOut ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockOut_CalculatesHoursCorrectly()
    {
        // Clock in at 09:00, clock out at 17:00 → 8 hours
        var jc = new JobClock
        {
            JobcPlanid = 1001, JobcOp = 1, JobcEmpnbr = "EMP-001",
            JobcDate   = "2026-03-04", JobcTimein = "09:00:00",
            JobcSite   = "DEFAULT",   JobcPosted = false
        };
        _db.JobClocks.Add(jc);
        await _db.SaveChangesAsync();

        // Manually set timeout to simulate 17:00
        jc.JobcTimeout = "17:00:00";
        await _db.SaveChangesAsync();

        var result = await _ctrl.ClockOut(jc.Id, new ClockOutRequest(Qty: 10m));

        result.Should().BeOfType<OkObjectResult>();
        var updated = _db.JobClocks.Find(jc.Id)!;
        updated.JobcHours.Should().BeApproximately(8m, 0.01m);
        updated.JobcQty.Should().Be(10m);
    }

    [Fact]
    public async Task ClockOut_NonExistent_Returns404()
    {
        var result = await _ctrl.ClockOut(99999, new ClockOutRequest(0m));
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ClockOut_SetsQuantityCompleted()
    {
        var jc = new JobClock
        {
            JobcPlanid = 1001, JobcOp = 1, JobcEmpnbr = "EMP-001",
            JobcDate   = "2026-03-04", JobcTimein = "08:00:00",
            JobcTimeout = "12:00:00",
            JobcSite   = "DEFAULT",   JobcPosted = false
        };
        _db.JobClocks.Add(jc);
        await _db.SaveChangesAsync();

        await _ctrl.ClockOut(jc.Id, new ClockOutRequest(Qty: 25m));

        _db.JobClocks.Find(jc.Id)!.JobcQty.Should().Be(25m);
    }

    [Fact]
    public async Task ClockOut_ZeroQty_StillCalculatesHours()
    {
        var jc = new JobClock
        {
            JobcPlanid = 1001, JobcOp = 1, JobcEmpnbr = "EMP-001",
            JobcDate   = "2026-03-04", JobcTimein = "10:00:00",
            JobcTimeout = "12:30:00",
            JobcSite   = "DEFAULT",   JobcPosted = false
        };
        _db.JobClocks.Add(jc);
        await _db.SaveChangesAsync();

        await _ctrl.ClockOut(jc.Id, new ClockOutRequest(Qty: 0m));

        // 10:00 → 12:30 = 2.5 hours
        _db.JobClocks.Find(jc.Id)!.JobcHours.Should().BeApproximately(2.5m, 0.01m);
    }

    private static void SetUser(ControllerBase ctrl, string username)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, username) }, "test"))
            }
        };
    }
}