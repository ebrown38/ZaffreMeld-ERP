using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Tests.Unit;

public class AppServiceTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly ZaffreMeldAppService _svc;

    public AppServiceTests()
    {
        _db  = TestDbFactory.CreateSeeded();
        _svc = new ZaffreMeldAppService(_db, TestConfig.Create(), NullLogger<ZaffreMeldAppService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Config ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GetSite_ReturnsConfiguredSite()
    {
        _svc.GetSite().Should().Be("DEFAULT");
    }

    [Fact]
    public void GetSite_MissingConfig_ReturnsFallback()
    {
        var svc = new ZaffreMeldAppService(
            _db,
            TestConfig.Create(new Dictionary<string, string?> { ["ZaffreMeld:SiteName"] = null }),
            NullLogger<ZaffreMeldAppService>.Instance);

        svc.GetSite().Should().Be("DEFAULT");
    }

    [Fact]
    public void GetVersion_ReturnsConfiguredVersion()
    {
        _svc.GetVersion().Should().Be("7.0");
    }

    // ── GetNextDocumentNumber ──────────────────────────────────────────────────

    [Fact]
    public async Task GetNextDocumentNumber_IncrementsCounter()
    {
        var first  = await _svc.GetNextDocumentNumber("SO");
        var second = await _svc.GetNextDocumentNumber("SO");

        first.Should().Be("SO-001001");
        second.Should().Be("SO-001002");
    }

    [Fact]
    public async Task GetNextDocumentNumber_RespectsPrefix()
    {
        var num = await _svc.GetNextDocumentNumber("SO");
        num.Should().StartWith("SO-");
    }

    [Fact]
    public async Task GetNextDocumentNumber_PadsToCounterLength()
    {
        // Counter has length=6, so value padded to 6 digits
        var num = await _svc.GetNextDocumentNumber("SO");
        // "SO-001001" → numeric part is "001001" (6 digits)
        var numericPart = num.Replace("SO-", "");
        numericPart.Should().HaveLength(6);
    }

    [Fact]
    public async Task GetNextDocumentNumber_PersistsIncrement()
    {
        await _svc.GetNextDocumentNumber("SO");

        // Re-read counter from db directly
        var counter = _db.Counters.First(c => c.CounterName == "SO");
        counter.CounterValue.Should().Be(1001);
    }

    [Fact]
    public async Task GetNextDocumentNumber_UnknownCounter_ReturnsFallback()
    {
        var num = await _svc.GetNextDocumentNumber("UNKNOWN");

        num.Should().StartWith("UNKNOWN-");
        // Fallback includes ticks — just verify it doesn't blow up
        num.Length.Should().BeGreaterThan(8);
    }

    [Fact]
    public async Task GetNextDocumentNumber_ConcurrentCalls_ProduceUniqueNumbers()
    {
        // Fire 10 concurrent requests — semaphore must prevent duplicates
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _svc.GetNextDocumentNumber("SO"));

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyHaveUniqueItems();
        results.Should().HaveCount(10);
    }

    // ── LogChange ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogChange_PersistsAuditRecord()
    {
        var result = await _svc.LogChange(
            user: "testuser", site: "DEFAULT",
            table: "SoMstr",  key: "SO-001",
            action: "UPDATE", field: "SoStatus",
            oldVal: "O",      newVal: "C");

        result.Success.Should().BeTrue();

        var log = _db.ChangeLogs.Single(l => l.ClKey == "SO-001");
        log.ClUser.Should().Be("testuser");
        log.ClTable.Should().Be("SoMstr");
        log.ClAction.Should().Be("UPDATE");
        log.ClField.Should().Be("SoStatus");
        log.ClOldValue.Should().Be("O");
        log.ClNewValue.Should().Be("C");
    }

    [Fact]
    public async Task LogChange_SetsTimestampToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _svc.LogChange("u", "DEFAULT", "T", "K", "A", "F", "", "");

        var log = _db.ChangeLogs.OrderByDescending(l => l.ClTimestamp).First();
        log.ClTimestamp.Should().BeAfter(before);
        log.ClTimestamp.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task LogChange_MultipleRecords_AllPersisted()
    {
        await _svc.LogChange("u", "DEFAULT", "T", "K1", "CREATE", "F", "", "X");
        await _svc.LogChange("u", "DEFAULT", "T", "K2", "UPDATE", "F", "X", "Y");
        await _svc.LogChange("u", "DEFAULT", "T", "K3", "DELETE", "F", "Y", "");

        _db.ChangeLogs.Count(l => l.ClTable == "T").Should().Be(3);
    }
}
