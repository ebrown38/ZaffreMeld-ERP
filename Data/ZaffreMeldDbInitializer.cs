using ZaffreMeld.Web.Models.Administration;
using Microsoft.AspNetCore.Identity;

namespace ZaffreMeld.Web.Data;

/// <summary>
/// Seeds the database with initial roles, admin user, and default config.
/// Mirrors the original Java loginInit / site bootstrap logic.
/// </summary>
public static class ZaffreMeldDbInitializer
{
    public static async Task SeedAsync(
        ZaffreMeldDbContext db,
        UserManager<ZaffreMeldUser> userManager,
        RoleManager<ZaffreMeldRole> roleManager)
    {
        // ── Roles ──────────────────────────────────────────────────────────────
        string[] roles = ["admin", "user", "api", "viewer",
                          "finance", "inventory", "orders", "purchasing",
                          "shipping", "hr", "edi", "production"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ZaffreMeldRole
                {
                    Name = role,
                    Description = $"ZaffreMeld {role} role",
                    IsSystemRole = role is "admin" or "api"
                });
            }
        }

        // ── Default admin user ─────────────────────────────────────────────────
        const string adminEmail = "admin@zaffremeld.local";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ZaffreMeldUser
            {
                UserName = "admin",
                Email = adminEmail,
                UserId = "admin",
                UserSite = "DEFAULT",
                FirstName = "System",
                LastName = "Admin",
                UserType = "admin",
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, "Admin1234!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "admin");
            }
        }

        // ── Default site ───────────────────────────────────────────────────────
        if (!db.Sites.Any())
        {
            db.Sites.Add(new SiteMstr
            {
                SiteSite = "DEFAULT",
                SiteDesc = "Default Site",
                SiteLine1 = "123 Main Street",
                SiteCity = "Anytown",
                SiteState = "TX",
                SiteZip = "75001",
                SiteCountry = "US",
                SiteCurrency = "USD",
                SiteActive = "1"
            });
        }

        // ── Default GL control ─────────────────────────────────────────────────
        if (!db.GlCtrl.Any())
        {
            db.GlCtrl.Add(new Models.Finance.GlCtrl
            {
                GlBsFrom = "1000",
                GlBsTo = "3999",
                GlIsFrom = "4000",
                GlIsTo = "9999",
                GlRetainedEarnings = "3900",
                GlSite = "DEFAULT"
            });
        }

        // ── Currency master ────────────────────────────────────────────────────
        if (!db.CurrMstr.Any())
        {
            db.CurrMstr.Add(new Models.Finance.CurrMstr
            {
                Id = "USD",
                Desc = "US Dollar",
                Symbol = "$",
                DecimalPlaces = 2,
                IsBase = true
            });
        }

        // ── UOM master ─────────────────────────────────────────────────────────
        if (!db.UomMstr.Any())
        {
            var uoms = new[]
            {
                new Models.Inventory.UomMstr { UomId = "EA", UomDesc = "Each", UomConvFactor = 1 },
                new Models.Inventory.UomMstr { UomId = "CS", UomDesc = "Case", UomConvFactor = 1 },
                new Models.Inventory.UomMstr { UomId = "LB", UomDesc = "Pound", UomConvFactor = 1 },
                new Models.Inventory.UomMstr { UomId = "FT", UomDesc = "Foot", UomConvFactor = 1 },
                new Models.Inventory.UomMstr { UomId = "HR", UomDesc = "Hour", UomConvFactor = 1 },
            };
            db.UomMstr.AddRange(uoms);
        }

        // ── Document counters ──────────────────────────────────────────────────
        if (!db.Counters.Any())
        {
            var counters = new[]
            {
                new Counter { CounterName = "SO", CounterDesc = "Sales Order", CounterPrefix = "SO-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
                new Counter { CounterName = "PO", CounterDesc = "Purchase Order", CounterPrefix = "PO-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
                new Counter { CounterName = "SH", CounterDesc = "Shipper", CounterPrefix = "SH-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
                new Counter { CounterName = "RV", CounterDesc = "Receiver", CounterPrefix = "RV-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
                new Counter { CounterName = "IN", CounterDesc = "Invoice", CounterPrefix = "IN-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
                new Counter { CounterName = "AP", CounterDesc = "AP Voucher", CounterPrefix = "AP-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
                new Counter { CounterName = "WO", CounterDesc = "Work Order", CounterPrefix = "WO-", CounterValue = 1000, CounterLength = 7, CounterSite = "DEFAULT" },
            };
            db.Counters.AddRange(counters);
        }

        await db.SaveChangesAsync();
    }
}
