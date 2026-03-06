using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZaffreMeld.Tests.Infrastructure;
using ZaffreMeld.Web.Controllers.Api;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Orders;
using System.Security.Claims;
using ZaffreMeld.Web.Services;
using Moq;

namespace ZaffreMeld.Tests.Unit;

/// <summary>
/// Tests the effective price lookup logic in OrdersController.GetEffectivePrice().
/// This mirrors the original Java PriceCalcUtil logic — finding the best matching
/// price tier based on customer, item, quantity, and effective dates.
/// </summary>
public class PricingLogicTests : IDisposable
{
    private readonly ZaffreMeldDbContext _db;
    private readonly OrdersController _ctrl;
    private readonly string _today;

    public PricingLogicTests()
    {
        _db   = TestDbFactory.CreateSeeded();
        _today = DateTime.Today.ToString("yyyy-MM-dd");

        var ordersMock = new Mock<IOrderService>();
        _ctrl = new OrdersController(
            ordersMock.Object,
            _db,
            NullLogger<OrdersController>.Instance);
        SetUser(_ctrl, "testuser");
    }

    public void Dispose() => _db.Dispose();

    // ── Basic price lookup ─────────────────────────────────────────────────────

    [Fact]
    public void GetEffectivePrice_MatchingPrice_ReturnsIt()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 10m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeTrue();
        ((decimal)body!.price).Should().Be(15.00m);
    }

    [Fact]
    public void GetEffectivePrice_NoMatchingPrice_ReturnsNotFound()
    {
        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeFalse();
        // price will be null when not found
    }

    [Fact]
    public void GetEffectivePrice_WrongCustomer_ReturnsNotFound()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("GLOBEX", "WIDGET-100", 10m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeFalse();
    }

    [Fact]
    public void GetEffectivePrice_WrongItem_ReturnsNotFound()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "GADGET-200", 10m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeFalse();
    }

    // ── Min quantity tiers ─────────────────────────────────────────────────────

    [Fact]
    public void GetEffectivePrice_QuantityMeetsLowestTier_ReturnsStandardPrice()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);
        SeedPrice("ACME", "WIDGET-100", 12.00m, minQty: 50m);
        SeedPrice("ACME", "WIDGET-100", 10.00m, minQty: 200m);

        // Qty=5 — only qualifies for minQty=1 tier
        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 5m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((decimal)body!.price).Should().Be(15.00m);
    }

    [Fact]
    public void GetEffectivePrice_QuantityMeetsMidTier_ReturnsMidPrice()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);
        SeedPrice("ACME", "WIDGET-100", 12.00m, minQty: 50m);
        SeedPrice("ACME", "WIDGET-100", 10.00m, minQty: 200m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 75m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((decimal)body!.price).Should().Be(12.00m);
    }

    [Fact]
    public void GetEffectivePrice_QuantityMeetsHighestTier_ReturnsBestPrice()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);
        SeedPrice("ACME", "WIDGET-100", 12.00m, minQty: 50m);
        SeedPrice("ACME", "WIDGET-100", 10.00m, minQty: 200m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 250m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((decimal)body!.price).Should().Be(10.00m);
    }

    [Fact]
    public void GetEffectivePrice_QuantityExactlyAtTierBoundary_QualifiesForTier()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);
        SeedPrice("ACME", "WIDGET-100", 12.00m, minQty: 50m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 50m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((decimal)body!.price).Should().Be(12.00m);
    }

    [Fact]
    public void GetEffectivePrice_QuantityOneBelowTier_DoesNotQualify()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);
        SeedPrice("ACME", "WIDGET-100", 12.00m, minQty: 50m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 49m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((decimal)body!.price).Should().Be(15.00m);
    }

    // ── Effective date filtering ───────────────────────────────────────────────

    [Fact]
    public void GetEffectivePrice_PriceInDateRange_ReturnsIt()
    {
        SeedPrice("ACME", "WIDGET-100", 14.00m, minQty: 1m,
            effFrom: "2026-01-01", effThru: "2026-12-31");

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeTrue();
        ((decimal)body!.price).Should().Be(14.00m);
    }

    [Fact]
    public void GetEffectivePrice_ExpiredPrice_NotReturned()
    {
        SeedPrice("ACME", "WIDGET-100", 9.99m, minQty: 1m,
            effFrom: "2020-01-01", effThru: "2020-12-31"); // expired

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeFalse();
    }

    [Fact]
    public void GetEffectivePrice_FuturePrice_NotReturned()
    {
        SeedPrice("ACME", "WIDGET-100", 9.99m, minQty: 1m,
            effFrom: "2099-01-01", effThru: "2099-12-31"); // future

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeFalse();
    }

    [Fact]
    public void GetEffectivePrice_EmptyDateRange_AlwaysValid()
    {
        // Empty effFrom / effThru means always active
        SeedPrice("ACME", "WIDGET-100", 13.00m, minQty: 1m, effFrom: "", effThru: "");

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeTrue();
    }

    // ── Inactive prices ────────────────────────────────────────────────────────

    [Fact]
    public void GetEffectivePrice_InactivePrice_NotReturned()
    {
        _db.CprMstr.Add(new CprMstr
        {
            CprCust    = "ACME",
            CprItem    = "WIDGET-100",
            CprPrice   = 11.00m,
            CprUom     = "EA",
            CprMinqty  = 1m,
            CprCurrency = "USD",
            CprEfffrom  = "",
            CprEffthru  = "",
            CprActive   = false   // <-- inactive
        });
        _db.SaveChanges();

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((bool)body!.found).Should().BeFalse();
    }

    // ── Response structure ─────────────────────────────────────────────────────

    [Fact]
    public void GetEffectivePrice_ReturnsCorrectCustAndItemInResponse()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m);

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 5m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((string)body!.cust).Should().Be("ACME");
        ((string)body!.item).Should().Be("WIDGET-100");
        ((decimal)body!.qty).Should().Be(5m);
    }

    [Fact]
    public void GetEffectivePrice_ReturnsUomFromPrice()
    {
        SeedPrice("ACME", "WIDGET-100", 15.00m, minQty: 1m, uom: "CS");

        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "WIDGET-100", 5m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((string)body!.uom).Should().Be("CS");
    }

    [Fact]
    public void GetEffectivePrice_DefaultsCurrencyToUsd_WhenNoPrice()
    {
        var result = _ctrl.GetEffectivePrice(new PriceRequest("ACME", "GHOST-ITEM", 1m)) as OkObjectResult;
        var body   = result!.Value as dynamic;

        ((string)body!.currency).Should().Be("USD");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SeedPrice(
        string cust, string item, decimal price,
        decimal minQty = 1m, string uom = "EA",
        string effFrom = "", string effThru = "")
    {
        _db.CprMstr.Add(new CprMstr
        {
            CprCust     = cust,
            CprItem     = item,
            CprPrice    = price,
            CprUom      = uom,
            CprMinqty   = minQty,
            CprCurrency = "USD",
            CprEfffrom  = effFrom,
            CprEffthru  = effThru,
            CprActive   = true
        });
        _db.SaveChanges();
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
