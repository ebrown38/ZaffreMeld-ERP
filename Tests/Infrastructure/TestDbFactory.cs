using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Models.Orders;

namespace ZaffreMeld.Tests.Infrastructure;

/// <summary>
/// Creates isolated in-memory DbContext instances for each test.
/// Each call returns a fresh database so tests do not share state.
/// </summary>
public static class TestDbFactory
{
    public static ZaffreMeldDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<ZaffreMeldDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new ZaffreMeldDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>Creates a db seeded with standard reference data used by many tests.</summary>
    public static ZaffreMeldDbContext CreateSeeded(string? dbName = null)
    {
        var db = Create(dbName);

        // Chart of accounts
        db.AcctMstr.AddRange(
            new AcctMstr { Id = "1000", Desc = "Cash",             Type = "A", Site = "DEFAULT" },
            new AcctMstr { Id = "1100", Desc = "Accounts Rec",     Type = "A", Site = "DEFAULT" },
            new AcctMstr { Id = "2000", Desc = "Accounts Pay",     Type = "L", Site = "DEFAULT" },
            new AcctMstr { Id = "4000", Desc = "Sales Revenue",    Type = "R", Site = "DEFAULT" },
            new AcctMstr { Id = "5000", Desc = "COGS",             Type = "X", Site = "DEFAULT" }
        );

        // Items
        db.ItemMstr.AddRange(
            new ItemMstr { ItItem = "WIDGET-100", ItDesc = "Blue Widget",   ItSite = "DEFAULT", ItStatus = "A", ItQoh = 100, ItType = "P" },
            new ItemMstr { ItItem = "GADGET-200", ItDesc = "Premium Gadget",ItSite = "DEFAULT", ItStatus = "A", ItQoh = 50,  ItType = "P" },
            new ItemMstr { ItItem = "OBSOLETE",   ItDesc = "Old Part",      ItSite = "DEFAULT", ItStatus = "I", ItQoh = 0,   ItType = "P" }
        );

        // Item costs
        db.ItemCost.AddRange(
            new ItemCost { ItcItem = "WIDGET-100", ItcSite = "DEFAULT", ItcSet = "STD", ItcTotalcost = 12.50m },
            new ItemCost { ItcItem = "GADGET-200", ItcSite = "DEFAULT", ItcSet = "STD", ItcTotalcost = 35.00m }
        );

        // Customers
        db.CmMstr.AddRange(
            new CmMstr { CmCode = "ACME",   CmName = "Acme Corporation",   CmStatus = "A", CmCrtdate = "2024-01-01" },
            new CmMstr { CmCode = "GLOBEX", CmName = "Globex Industries",  CmStatus = "A", CmCrtdate = "2024-01-01" },
            new CmMstr { CmCode = "INACTIVE-CO", CmName = "Old Customer",  CmStatus = "I", CmCrtdate = "2020-01-01" }
        );

        // Counter
        db.Counters.Add(new Counter
        {
            CounterName   = "SO",
            CounterPrefix = "SO-",
            CounterValue  = 1000,
            CounterLength = 6,
            CounterSite   = "DEFAULT"
        });

        db.SaveChanges();
        return db;
    }
}

/// <summary>
/// Reflection helpers for accessing anonymous-type properties returned by controllers.
/// Anonymous types defined in one assembly cannot be accessed via `dynamic` from another;
/// use Prop&lt;T&gt; instead.
/// </summary>
public static class Anon
{
    /// <summary>Gets a named property value from an anonymous/object type by reflection.</summary>
    public static T Prop<T>(object? obj, string name)
    {
        ArgumentNullException.ThrowIfNull(obj);
        var val = obj.GetType().GetProperty(name)?.GetValue(obj)
                  ?? throw new InvalidOperationException($"Property '{name}' not found on {obj.GetType().Name}");
        return (T)val;
    }
}

public static class TestConfig
{
    public static IConfiguration Create(Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["ZaffreMeld:SiteName"]  = "DEFAULT",
            ["ZaffreMeld:Version"]   = "7.0",
            ["Jwt:Key"]              = "ZaffreMeld-Test-Secret-Key-Min32Chars!!",
            ["Jwt:Issuer"]           = "ZaffreMeld",
            ["Jwt:Audience"]         = "ZaffreMeld",
            ["Jwt:ExpiryHours"]      = "8",
            ["Edi:OurIsaId"]         = "ZAFFREMELD     ",
            ["Edi:OurGsId"]          = "ZAFFREMELD"
        };

        if (overrides != null)
            foreach (var kv in overrides)
                defaults[kv.Key] = kv.Value;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }
}

/// <summary>Sample X12 850 Purchase Order used across EDI tests.</summary>
public static class SampleEdi
{
    public const string Edi850 =
        "ISA*00*          *00*          *ZZ*ACMEPARTNER    *ZZ*ZAFFREMELD     *260301*1200*^*00501*000000001*0*P*:~\n" +
        "GS*PO*ACMEPARTNER*ZAFFREMELD*20260301*1200*1*X*005010~\n" +
        "ST*850*0001~\n" +
        "BEG*00*SA*PO-98765**20260301~\n" +
        "REF*DP*DEPT-47~\n" +
        "DTM*002*20260315~\n" +
        "N1*BT*ACME CORPORATION*92*ACMEPARTNER~\n" +
        "N1*ST*ACME WAREHOUSE*92*ACMEPARTNER~\n" +
        "N3*100 INDUSTRIAL BLVD~\n" +
        "N4*HOUSTON*TX*77001~\n" +
        "PO1*1*10*EA*25.00*PE*BP*WIDGET-100*VN*WDG-100~\n" +
        "PID*F****Blue Widget 100 Pack~\n" +
        "PO1*2*5*EA*49.99*PE*BP*GADGET-200*VN*GDG-200~\n" +
        "PID*F****Premium Gadget Set~\n" +
        "CTT*2~\n" +
        "SE*15*0001~\n" +
        "GE*1*1~\n" +
        "IEA*1*000000001~";

    public const string Edi997 =
        "ISA*00*          *00*          *ZZ*ACMEPARTNER    *ZZ*ZAFFREMELD     *260301*1201*^*00501*000000002*0*P*:~\n" +
        "GS*FA*ACMEPARTNER*ZAFFREMELD*20260301*1201*2*X*005010~\n" +
        "ST*997*0001~\n" +
        "AK1*PO*1~\n" +
        "AK2*850*0001~\n" +
        "AK5*A~\n" +
        "AK9*A*1*1*1~\n" +
        "SE*6*0001~\n" +
        "GE*1*2~\n" +
        "IEA*1*000000002~";
}