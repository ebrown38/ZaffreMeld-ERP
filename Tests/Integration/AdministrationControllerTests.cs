using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Services;
using System.Security.Claims;
using ZaffreMeld.Tests.Infrastructure;

namespace ZaffreMeld.Tests.Integration;

public class AdministrationControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly Mock<IZaffreMeldAppService> _appMock;
    private readonly AdministrationController _ctrl;

    public AdministrationControllerTests()
    {
        _db      = TestDbFactory.CreateSeeded();
        _appMock = new Mock<IZaffreMeldAppService>();
        _appMock.Setup(a => a.GetNextDocumentNumber(It.IsAny<string>())).ReturnsAsync("SO-001001");

        _ctrl = new AdministrationController(_db, _appMock.Object, NullLogger<AdministrationController>.Instance);
        SetUser(_ctrl, "admin");

        // Seed sites
        _db.Sites.AddRange(
            new SiteMstr { SiteSite = "DEFAULT", SiteDesc = "Main Site",  SiteActive = "1" },
            new SiteMstr { SiteSite = "WEST",    SiteDesc = "West Coast", SiteActive = "1" }
        );

        // Seed code master entries
        _db.CodeMstr.AddRange(
            new CodeMstr { CodeCode = "TERMS",   CodeKey = "NET30", CodeValue = "Net 30 Days", CodeDesc = "Net 30", CodeActive = "1" },
            new CodeMstr { CodeCode = "TERMS",   CodeKey = "NET60", CodeValue = "Net 60 Days", CodeDesc = "Net 60", CodeActive = "1" },
            new CodeMstr { CodeCode = "CARRIER", CodeKey = "FEDEX", CodeValue = "FedEx",       CodeDesc = "FedEx",  CodeActive = "1" }
        );

        // Seed menu items
        _db.MenuMstr.AddRange(
            new MenuMstr { MenuId = "MENU-001", MenuParent = "",         MenuDesc = "Finance",    MenuProgram = "/finance",    MenuRole = "finance", MenuActive = "1", MenuSeq = 1 },
            new MenuMstr { MenuId = "MENU-002", MenuParent = "MENU-001", MenuDesc = "GL",         MenuProgram = "/finance/gl", MenuRole = "finance", MenuActive = "1", MenuSeq = 1 },
            new MenuMstr { MenuId = "MENU-003", MenuParent = "",         MenuDesc = "Hidden",     MenuProgram = "/hidden",     MenuRole = "admin",   MenuActive = "0", MenuSeq = 99 }
        );

        // Seed audit logs
        _db.ChangeLogs.AddRange(
            new ChangeLog { ClUser = "admin", ClSite = "DEFAULT", ClTable = "SoMstr", ClKey = "SO-001", ClAction = "CREATE", ClField = "", ClOldValue = "", ClNewValue = "", ClTimestamp = DateTime.UtcNow.AddDays(-1) },
            new ChangeLog { ClUser = "user1", ClSite = "DEFAULT", ClTable = "ItemMstr", ClKey = "WIDGET-100", ClAction = "UPDATE", ClField = "ItDesc", ClOldValue = "Old", ClNewValue = "New", ClTimestamp = DateTime.UtcNow }
        );

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Sites ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GetSites_ReturnsBothSites_OrderedBySiteCode()
    {
        var result = _ctrl.GetSites() as OkObjectResult;
        var sites  = (List<SiteMstr>)result!.Value!;
        sites.Should().HaveCount(2);
        sites[0].SiteSite.Should().Be("DEFAULT");
        sites[1].SiteSite.Should().Be("WEST");
    }

    [Fact]
    public void GetSite_ExistingId_Returns200()
    {
        var result = _ctrl.GetSite("DEFAULT") as OkObjectResult;
        ((SiteMstr)result!.Value!).SiteDesc.Should().Be("Main Site");
    }

    [Fact]
    public void GetSite_NonExistent_Returns404()
    {
        _ctrl.GetSite("GHOST").Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AddSite_NewSite_Returns200AndPersists()
    {
        var site = new SiteMstr { SiteSite = "EAST", SiteDesc = "East Coast", SiteActive = "1" };

        var result = await _ctrl.AddSite(site);

        result.Should().BeOfType<OkObjectResult>();
        _db.Sites.Find("EAST").Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSite_ChangesDescription()
    {
        var site = _db.Sites.Find("DEFAULT")!;
        site.SiteDesc = "Main Site (Updated)";

        await _ctrl.UpdateSite("DEFAULT", site);

        _db.Sites.Find("DEFAULT")!.SiteDesc.Should().Be("Main Site (Updated)");
    }

    // ── Code Master ────────────────────────────────────────────────────────────

    [Fact]
    public void GetCodes_NoFilter_ReturnsAll()
    {
        var result = _ctrl.GetCodes() as OkObjectResult;
        var codes  = (List<CodeMstr>)result!.Value!;
        codes.Should().HaveCount(3);
    }

    [Fact]
    public void GetCodes_FilterByCode_ReturnsMatchingGroup()
    {
        var result = _ctrl.GetCodes(code: "TERMS") as OkObjectResult;
        var codes  = (List<CodeMstr>)result!.Value!;
        codes.Should().HaveCount(2);
        codes.Should().OnlyContain(c => c.CodeCode == "TERMS");
    }

    [Fact]
    public void GetCodes_FilterByCode_OrderedByCodeThenKey()
    {
        var result = _ctrl.GetCodes(code: "TERMS") as OkObjectResult;
        var codes  = (List<CodeMstr>)result!.Value!;
        codes[0].CodeKey.Should().Be("NET30");
        codes[1].CodeKey.Should().Be("NET60");
    }

    [Fact]
    public async Task AddCode_NewCode_Persists()
    {
        var cm = new CodeMstr { CodeCode = "TERMS", CodeKey = "COD", CodeValue = "Cash on Delivery", CodeDesc = "COD", CodeActive = "1" };

        var result = await _ctrl.AddCode(cm);

        result.Should().BeOfType<OkObjectResult>();
        _db.CodeMstr.Count(c => c.CodeCode == "TERMS").Should().Be(3);
    }

    [Fact]
    public async Task DeleteCode_ExistingCode_Returns200AndRemoves()
    {
        var result = await _ctrl.DeleteCode("CARRIER", "FEDEX");

        result.Should().BeOfType<OkObjectResult>();
        _db.CodeMstr.Find("CARRIER", "FEDEX").Should().BeNull();
    }

    [Fact]
    public async Task DeleteCode_NonExistent_Returns404()
    {
        (await _ctrl.DeleteCode("GHOST", "KEY")).Should().BeOfType<NotFoundResult>();
    }

    // ── Menu ───────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMenu_ExcludesInactiveItems()
    {
        var result = _ctrl.GetMenu() as OkObjectResult;
        var items  = (List<MenuMstr>)result!.Value!;
        items.Should().NotContain(m => m.MenuId == "MENU-003"); // active=0
    }

    [Fact]
    public void GetMenu_FiltersByRole()
    {
        var result = _ctrl.GetMenu(role: "finance") as OkObjectResult;
        var items  = (List<MenuMstr>)result!.Value!;
        // "finance" role gets items where role=="finance" OR role==""
        items.Should().OnlyContain(m => m.MenuRole == "finance" || m.MenuRole == "");
    }

    [Fact]
    public void GetMenu_OrdersByParentThenSeq()
    {
        var result = _ctrl.GetMenu() as OkObjectResult;
        var items  = (List<MenuMstr>)result!.Value!;
        // Parent "" should come before "MENU-001"
        items.First().MenuParent.Should().Be("");
    }

    // ── Counters ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetCounters_ReturnsSeededCounter()
    {
        var result = _ctrl.GetCounters() as OkObjectResult;
        var counters = (List<Counter>)result!.Value!;
        counters.Should().Contain(c => c.CounterName == "SO");
    }

    [Fact]
    public async Task GetNextNumber_ReturnsFromAppService()
    {
        var result = await _ctrl.GetNextNumber("SO") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((string)body!.next).Should().Be("SO-001001");
        _appMock.Verify(a => a.GetNextDocumentNumber("SO"), Times.Once);
    }

    // ── Users ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GetUsers_ReturnsUserList()
    {
        // Identity tables may be empty in test db, just verify no crash
        var result = _ctrl.GetUsers();
        result.Should().BeOfType<OkObjectResult>();
    }

    // ── Audit Log ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetChangeLog_ReturnsAllLogs()
    {
        var result = _ctrl.GetChangeLog() as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(2);
    }

    [Fact]
    public void GetChangeLog_FiltersByTable()
    {
        var result = _ctrl.GetChangeLog(table: "SoMstr") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    [Fact]
    public void GetChangeLog_FiltersByUser()
    {
        var result = _ctrl.GetChangeLog(user: "user1") as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(1);
    }

    [Fact]
    public void GetChangeLog_ReturnsNewestFirst()
    {
        var result  = _ctrl.GetChangeLog() as OkObjectResult;
        var body    = result!.Value as dynamic;
        var logs    = (List<ChangeLog>)body!.logs;
        logs[0].ClTable.Should().Be("ItemMstr"); // newer
        logs[1].ClTable.Should().Be("SoMstr");   // older
    }

    [Fact]
    public void GetChangeLog_Pagination_RespectsPageSize()
    {
        var result = _ctrl.GetChangeLog(pageSize: 1) as OkObjectResult;
        var body   = result!.Value as dynamic;
        ((int)body!.total).Should().Be(2);        // total unchanged
        ((List<ChangeLog>)body!.logs).Should().HaveCount(1); // only 1 page
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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
