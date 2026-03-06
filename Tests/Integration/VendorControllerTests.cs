using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Vendor;
using System.Security.Claims;

namespace ZaffreMeld.Tests.Integration;

public class VendorControllerTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly VendorController _ctrl;

    public VendorControllerTests()
    {
        _db  = TestDbFactory.Create();
        _ctrl = new VendorController(_db, NullLogger<VendorController>.Instance);
        SetUser(_ctrl, "testuser");

        // Seed vendors
        _db.VdMstr.AddRange(
            new VdMstr { VdAddr = "ACME-SUPPLY",  VdName = "Acme Supply Co",   VdStatus = "A", VdCurrency = "USD", VdCrtdate = "2024-01-01" },
            new VdMstr { VdAddr = "WIDGET-WORLD", VdName = "Widget World Inc",  VdStatus = "A", VdCurrency = "USD", VdCrtdate = "2024-01-01" },
            new VdMstr { VdAddr = "INACTIVE-VEND", VdName = "Old Vendor",       VdStatus = "I", VdCurrency = "USD", VdCrtdate = "2020-01-01" }
        );
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetVendor ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetVendor_ExistingId_Returns200()
    {
        var result = _ctrl.GetVendor("ACME-SUPPLY") as OkObjectResult;
        result.Should().NotBeNull();
        ((VdMstr)result!.Value!).VdName.Should().Be("Acme Supply Co");
    }

    [Fact]
    public void GetVendor_NonExistent_Returns404()
    {
        _ctrl.GetVendor("GHOST-VEND").Should().BeOfType<NotFoundResult>();
    }

    // ── SearchVendors ──────────────────────────────────────────────────────────

    [Fact]
    public void SearchVendors_ByName_ReturnsMatch()
    {
        var result = _ctrl.SearchVendors("Acme") as OkObjectResult;
        var list   = (List<VdMstr>)result!.Value!;
        list.Should().HaveCount(1);
        list.Single().VdAddr.Should().Be("ACME-SUPPLY");
    }

    [Fact]
    public void SearchVendors_ByCode_ReturnsMatch()
    {
        var result = _ctrl.SearchVendors("WIDGET") as OkObjectResult;
        var list   = (List<VdMstr>)result!.Value!;
        list.Should().HaveCount(1);
    }

    [Fact]
    public void SearchVendors_ExcludesInactive()
    {
        // SearchVendors filters on VdStatus == "A"
        var result = _ctrl.SearchVendors("") as OkObjectResult;
        var list   = (List<VdMstr>)result!.Value!;
        list.Should().NotContain(v => v.VdAddr == "INACTIVE-VEND");
    }

    [Fact]
    public void SearchVendors_NoMatch_ReturnsEmpty()
    {
        var result = _ctrl.SearchVendors("ZZNOTFOUND") as OkObjectResult;
        ((List<VdMstr>)result!.Value!).Should().BeEmpty();
    }

    [Fact]
    public void SearchVendors_RespectsMaxResults()
    {
        var result = _ctrl.SearchVendors("", max: 1) as OkObjectResult;
        ((List<VdMstr>)result!.Value!).Should().HaveCount(1);
    }

    // ── AddVendor ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddVendor_NewVendor_Returns200AndPersists()
    {
        var vendor = new VdMstr { VdAddr = "NEW-VEND", VdName = "New Vendor LLC", VdStatus = "A", VdCurrency = "USD" };

        var result = await _ctrl.AddVendor(vendor);

        result.Should().BeOfType<OkObjectResult>();
        _db.VdMstr.Find("NEW-VEND").Should().NotBeNull();
    }

    [Fact]
    public async Task AddVendor_SetsCreatedDateToToday()
    {
        var vendor = new VdMstr { VdAddr = "DATED-VEND", VdName = "Dated Vendor", VdStatus = "A", VdCurrency = "USD" };
        await _ctrl.AddVendor(vendor);

        _db.VdMstr.Find("DATED-VEND")!.VdCrtdate.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task AddVendor_DuplicateId_Returns400()
    {
        var dup = new VdMstr { VdAddr = "ACME-SUPPLY", VdName = "Duplicate", VdStatus = "A", VdCurrency = "USD" };

        var result = await _ctrl.AddVendor(dup);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateVendor ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateVendor_ChangesName()
    {
        var vendor = _db.VdMstr.Find("ACME-SUPPLY")!;
        vendor.VdName = "Acme Supply (Updated)";

        var result = await _ctrl.UpdateVendor("ACME-SUPPLY", vendor);

        result.Should().BeOfType<OkObjectResult>();
        _db.VdMstr.Find("ACME-SUPPLY")!.VdName.Should().Be("Acme Supply (Updated)");
    }

    [Fact]
    public async Task UpdateVendor_IdMismatch_Returns400()
    {
        var vendor = new VdMstr { VdAddr = "WRONG-ID", VdName = "Mismatch" };
        var result = await _ctrl.UpdateVendor("ACME-SUPPLY", vendor);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetVendorPricing ───────────────────────────────────────────────────────

    [Fact]
    public void GetVendorPricing_WithPrices_ReturnsThem()
    {
        _db.VprMstr.AddRange(
            new VprMstr { VprVend = "ACME-SUPPLY", VprItem = "WIDGET-100", VprPrice = 12.50m, VprUom = "EA", VprMinqty = 1m, VprCurrency = "USD", VprActive = true },
            new VprMstr { VprVend = "ACME-SUPPLY", VprItem = "WIDGET-100", VprPrice = 11.00m, VprUom = "EA", VprMinqty = 100m, VprCurrency = "USD", VprActive = true }
        );
        _db.SaveChanges();

        var result = _ctrl.GetVendorPricing("ACME-SUPPLY") as OkObjectResult;
        ((List<VprMstr>)result!.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetVendorPricing_FiltersByItem()
    {
        _db.VprMstr.AddRange(
            new VprMstr { VprVend = "ACME-SUPPLY", VprItem = "WIDGET-100", VprPrice = 12.50m, VprUom = "EA", VprMinqty = 1m, VprCurrency = "USD", VprActive = true },
            new VprMstr { VprVend = "ACME-SUPPLY", VprItem = "GADGET-200", VprPrice = 25.00m, VprUom = "EA", VprMinqty = 1m, VprCurrency = "USD", VprActive = true }
        );
        _db.SaveChanges();

        var result = _ctrl.GetVendorPricing("ACME-SUPPLY", item: "WIDGET-100") as OkObjectResult;
        ((List<VprMstr>)result!.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetVendorPricing_ExcludesInactive()
    {
        _db.VprMstr.Add(new VprMstr { VprVend = "ACME-SUPPLY", VprItem = "WIDGET-100", VprPrice = 0m, VprUom = "EA", VprActive = false });
        _db.SaveChanges();

        var result = _ctrl.GetVendorPricing("ACME-SUPPLY") as OkObjectResult;
        ((List<VprMstr>)result!.Value!).Should().BeEmpty();
    }

    [Fact]
    public void GetVendorPricing_NoPrices_ReturnsEmpty()
    {
        var result = _ctrl.GetVendorPricing("WIDGET-WORLD") as OkObjectResult;
        ((List<VprMstr>)result!.Value!).Should().BeEmpty();
    }

    // ── GetVendorXref ──────────────────────────────────────────────────────────

    [Fact]
    public void GetVendorXref_WithXrefs_ReturnsThem()
    {
        _db.VdpMstr.Add(new VdpMstr { VdpVend = "ACME-SUPPLY", VdpItem = "WIDGET-100", VdpVitem = "WDG-100", VdpActive = true });
        _db.SaveChanges();

        var result = _ctrl.GetVendorXref("ACME-SUPPLY") as OkObjectResult;
        ((List<VdpMstr>)result!.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetVendorXref_ExcludesInactive()
    {
        _db.VdpMstr.Add(new VdpMstr { VdpVend = "ACME-SUPPLY", VdpItem = "OLD-ITEM", VdpVitem = "OLD", VdpActive = false });
        _db.SaveChanges();

        var result = _ctrl.GetVendorXref("ACME-SUPPLY") as OkObjectResult;
        ((List<VdpMstr>)result!.Value!).Should().BeEmpty();
    }

    // ── GetVendorShipTos ───────────────────────────────────────────────────────

    [Fact]
    public void GetVendorShipTos_WithShipTos_ReturnsThem()
    {
        _db.VdsDet.Add(new VdsDet { VdsCode = "ACME-SUPPLY", VdsShipto = "WH-01", VdsName = "Acme Warehouse", VdsCity = "Houston" });
        _db.SaveChanges();

        var result = _ctrl.GetVendorShipTos("ACME-SUPPLY") as OkObjectResult;
        ((List<VdsDet>)result!.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetVendorShipTos_NoShipTos_ReturnsEmpty()
    {
        var result = _ctrl.GetVendorShipTos("WIDGET-WORLD") as OkObjectResult;
        ((List<VdsDet>)result!.Value!).Should().BeEmpty();
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
