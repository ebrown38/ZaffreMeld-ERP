using Microsoft.EntityFrameworkCore;
using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Models.Finance;
using ZaffreMeld.Web.Models.Inventory;
using ZaffreMeld.Web.Models.Orders;

namespace ZaffreMeld.Web.Services;

// ── Result type (replaces Java String[] {bit, message} pattern) ───────────────

/// <summary>
/// Replaces the Java pattern of returning String[] { "0"/"1", "message" }.
/// "0" = success, "1" = error in original Java code.
/// </summary>
public record ServiceResult(bool Success, string Message, object? Data = null)
{
    public static ServiceResult Ok(string message = "Record saved successfully.", object? data = null)
        => new(true, message, data);

    public static ServiceResult Error(string message)
        => new(false, message);
}

// ── ZaffreMeld Application Service (shared state / config) ─────────────────────

/// <summary>
/// Provides application-wide ZaffreMeld configuration and utility functions.
/// Replaces the static fields on Java's bsmf.MainFrame.
/// </summary>
public interface IZaffreMeldAppService
{
    string GetSite();
    string GetVersion();
    Task<string> GetNextDocumentNumber(string counterName);
    Task<ServiceResult> LogChange(string user, string site, string table, string key, string action, string field, string oldVal, string newVal);
}

public class ZaffreMeldAppService : IZaffreMeldAppService
{
    private readonly ZaffreMeldDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<ZaffreMeldAppService> _logger;
    private static readonly SemaphoreSlim _counterLock = new(1, 1);

    public ZaffreMeldAppService(ZaffreMeldDbContext db, IConfiguration config, ILogger<ZaffreMeldAppService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public string GetSite() => _config["ZaffreMeld:SiteName"] ?? "DEFAULT";
    public string GetVersion() => _config["ZaffreMeld:Version"] ?? "7.0";

    public async Task<string> GetNextDocumentNumber(string counterName)
    {
        await _counterLock.WaitAsync();
        try
        {
            var counter = await _db.Counters
                .FirstOrDefaultAsync(c => c.CounterName == counterName && c.CounterSite == GetSite());

            if (counter == null)
                return $"{counterName}-{DateTime.Now.Ticks}";

            counter.CounterValue++;
            await _db.SaveChangesAsync();

            var number = counter.CounterValue.ToString().PadLeft(counter.CounterLength, '0');
            return $"{counter.CounterPrefix}{number}";
        }
        finally
        {
            _counterLock.Release();
        }
    }

    public async Task<ServiceResult> LogChange(string user, string site, string table, string key,
        string action, string field, string oldVal, string newVal)
    {
        try
        {
            _db.ChangeLogs.Add(new ChangeLog
            {
                ClUser = user,
                ClSite = site,
                ClTable = table,
                ClKey = key,
                ClAction = action,
                ClField = field,
                ClOldValue = oldVal,
                ClNewValue = newVal,
                ClTimestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log change for {Table} {Key}", table, key);
            return ServiceResult.Error(ex.Message);
        }
    }
}

// ── Finance Service ────────────────────────────────────────────────────────────

public interface IFinanceService
{
    Task<AcctMstr?> GetAccount(string id);
    Task<List<AcctMstr>> GetAccountsInRange(string from, string to);
    Task<ServiceResult> AddAccount(AcctMstr acct);
    Task<ServiceResult> UpdateAccount(AcctMstr acct);
    Task<ServiceResult> DeleteAccount(string id);
    Task<ServiceResult> PostGlPair(GlPair pair);
    Task<ServiceResult> PostGlTransaction(GlTran tran);
    Task<ServiceResult> PostGlTransactions(List<GlTran> trans);
    Task<List<GlTran>> GetGlTransactions(string account, string? fromDate = null, string? toDate = null);
    Task<decimal> GetAccountBalance(string account, string cc, string year, string period);
}

public class FinanceService : IFinanceService
{
    private readonly ZaffreMeldDbContext _db;
    private readonly ILogger<FinanceService> _logger;

    public FinanceService(ZaffreMeldDbContext db, ILogger<FinanceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AcctMstr?> GetAccount(string id)
        => await _db.AcctMstr.FindAsync(id);

    public async Task<List<AcctMstr>> GetAccountsInRange(string from, string to)
        => await _db.AcctMstr
            .Where(a => string.Compare(a.Id, from) >= 0 && string.Compare(a.Id, to) <= 0)
            .OrderBy(a => a.Id)
            .ToListAsync();

    public async Task<ServiceResult> AddAccount(AcctMstr acct)
    {
        try
        {
            if (await _db.AcctMstr.AnyAsync(a => a.Id == acct.Id))
                return ServiceResult.Error("Account already exists.");
            _db.AcctMstr.Add(acct);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Account added successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding account {Id}", acct.Id);
            return ServiceResult.Error($"SQL error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> UpdateAccount(AcctMstr acct)
    {
        try
        {
            _db.AcctMstr.Update(acct);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Account updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account {Id}", acct.Id);
            return ServiceResult.Error($"SQL error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> DeleteAccount(string id)
    {
        try
        {
            var acct = await _db.AcctMstr.FindAsync(id);
            if (acct == null) return ServiceResult.Error("Account not found.");
            _db.AcctMstr.Remove(acct);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Account deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account {Id}", id);
            return ServiceResult.Error($"SQL error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> PostGlPair(GlPair pair)
    {
        // Creates two balanced GL transactions (debit + credit)
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            _db.GlTran.Add(new GlTran
            {
                GltAcct = pair.GlvAcctDr, GltCc = pair.GlvCcDr,
                GltAmt = pair.GlvAmt, GltBaseAmt = pair.GlvBaseAmt,
                GltCurr = pair.GlvCurr, GltRef = pair.GlvRef,
                GltEffdate = pair.GlvEffdate.Length > 0 ? pair.GlvEffdate : today,
                GltType = pair.GlvType, GltDesc = pair.GlvDesc,
                GltDoc = pair.GlvDoc, GltSite = pair.GlvSite,
                GltEntdate = today
            });
            _db.GlTran.Add(new GlTran
            {
                GltAcct = pair.GlvAcctCr, GltCc = pair.GlvCcCr,
                GltAmt = -pair.GlvAmt, GltBaseAmt = -pair.GlvBaseAmt,
                GltCurr = pair.GlvCurr, GltRef = pair.GlvRef,
                GltEffdate = pair.GlvEffdate.Length > 0 ? pair.GlvEffdate : today,
                GltType = pair.GlvType, GltDesc = pair.GlvDesc,
                GltDoc = pair.GlvDoc, GltSite = pair.GlvSite,
                GltEntdate = today
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error posting GL pair");
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> PostGlTransaction(GlTran tran)
    {
        try
        {
            _db.GlTran.Add(tran);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting GL transaction");
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> PostGlTransactions(List<GlTran> trans)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.GlTran.AddRange(trans);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult.Ok($"{trans.Count} transactions posted.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error posting GL transactions");
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<List<GlTran>> GetGlTransactions(string account, string? fromDate = null, string? toDate = null)
    {
        var query = _db.GlTran.Where(t => t.GltAcct == account);
        if (fromDate != null) query = query.Where(t => string.Compare(t.GltEffdate, fromDate) >= 0);
        if (toDate != null) query = query.Where(t => string.Compare(t.GltEffdate, toDate) <= 0);
        return await query.OrderByDescending(t => t.GltEffdate).ToListAsync();
    }

    public async Task<decimal> GetAccountBalance(string account, string cc, string year, string period)
        => await _db.GlTran
            .Where(t => t.GltAcct == account && t.GltCc == cc &&
                        t.GltYear == year && t.GltPeriod == period)
            .SumAsync(t => t.GltAmt);
}

// ── Inventory Service ──────────────────────────────────────────────────────────

public interface IInventoryService
{
    Task<ItemMstr?> GetItem(string itemId);
    Task<List<ItemMstr>> SearchItems(string search, int maxResults = 50);
    Task<ServiceResult> AddItem(ItemMstr item);
    Task<ServiceResult> UpdateItem(ItemMstr item);
    Task<ServiceResult> DeleteItem(string itemId);
    Task<ItemCost?> GetItemCost(string item, string site, string set = "STD");
    Task<decimal> GetItemQoh(string item, string site);
}

public class InventoryService : IInventoryService
{
    private readonly ZaffreMeldDbContext _db;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(ZaffreMeldDbContext db, ILogger<InventoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ItemMstr?> GetItem(string itemId)
        => await _db.ItemMstr.FindAsync(itemId);

    public async Task<List<ItemMstr>> SearchItems(string search, int maxResults = 50)
        => await _db.ItemMstr
            .Where(i => i.ItItem.Contains(search) || i.ItDesc.Contains(search))
            .OrderBy(i => i.ItItem)
            .Take(maxResults)
            .ToListAsync();

    public async Task<ServiceResult> AddItem(ItemMstr item)
    {
        try
        {
            if (await _db.ItemMstr.AnyAsync(i => i.ItItem == item.ItItem))
                return ServiceResult.Error("Item already exists.");
            item.ItCrtdate = DateTime.Today.ToString("yyyy-MM-dd");
            _db.ItemMstr.Add(item);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Item added successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item {Item}", item.ItItem);
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> UpdateItem(ItemMstr item)
    {
        try
        {
            _db.ItemMstr.Update(item);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Item updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item {Item}", item.ItItem);
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> DeleteItem(string itemId)
    {
        try
        {
            var item = await _db.ItemMstr.FindAsync(itemId);
            if (item == null) return ServiceResult.Error("Item not found.");
            _db.ItemMstr.Remove(item);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Item deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item {Item}", itemId);
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ItemCost?> GetItemCost(string item, string site, string set = "STD")
        => await _db.ItemCost.FindAsync(item, site, set);

    public async Task<decimal> GetItemQoh(string item, string site)
        => await _db.ItemMstr
            .Where(i => i.ItItem == item && i.ItSite == site)
            .Select(i => i.ItQoh)
            .FirstOrDefaultAsync();
}

// ── Order Service ──────────────────────────────────────────────────────────────

public interface IOrderService
{
    Task<SoMstr?> GetSalesOrder(string soNbr);
    Task<List<SodDet>> GetSalesOrderLines(string soNbr);
    Task<ServiceResult> CreateSalesOrder(SoMstr so, List<SodDet> lines);
    Task<ServiceResult> UpdateSalesOrder(SoMstr so);
    Task<ServiceResult> CloseOrder(string soNbr);
    Task<List<SoMstr>> GetOpenOrders(string? custFilter = null);
    Task<CmMstr?> GetCustomer(string custCode);
    Task<List<CmMstr>> SearchCustomers(string search, int maxResults = 50);
    Task<ServiceResult> AddCustomer(CmMstr cust);
    Task<ServiceResult> UpdateCustomer(CmMstr cust);
}

public class OrderService : IOrderService
{
    private readonly ZaffreMeldDbContext _db;
    private readonly IZaffreMeldAppService _app;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ZaffreMeldDbContext db, IZaffreMeldAppService app, ILogger<OrderService> logger)
    {
        _db = db;
        _app = app;
        _logger = logger;
    }

    public async Task<SoMstr?> GetSalesOrder(string soNbr)
        => await _db.SoMstr.FindAsync(soNbr);

    public async Task<List<SodDet>> GetSalesOrderLines(string soNbr)
        => await _db.SodDet.Where(l => l.SodNbr == soNbr).OrderBy(l => l.SodLine).ToListAsync();

    public async Task<ServiceResult> CreateSalesOrder(SoMstr so, List<SodDet> lines)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrEmpty(so.SoNbr))
                so.SoNbr = await _app.GetNextDocumentNumber("SO");

            so.SoEntdate = DateTime.Today.ToString("yyyy-MM-dd");
            so.SoStatus = "O";

            int lineNum = 10;
            foreach (var line in lines)
            {
                line.SodNbr = so.SoNbr;
                if (line.SodLine == 0) { line.SodLine = lineNum; lineNum += 10; }
            }

            so.SoTotalamt = lines.Sum(l => l.SodQty * l.SodPrice * (1 - l.SodDisc / 100));

            _db.SoMstr.Add(so);
            _db.SodDet.AddRange(lines);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult.Ok("Sales order created.", so.SoNbr);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error creating sales order");
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> UpdateSalesOrder(SoMstr so)
    {
        try
        {
            _db.SoMstr.Update(so);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Sales order updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SO {So}", so.SoNbr);
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> CloseOrder(string soNbr)
    {
        try
        {
            var so = await _db.SoMstr.FindAsync(soNbr);
            if (so == null) return ServiceResult.Error("Order not found.");
            so.SoStatus = "C";
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Order closed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing SO {So}", soNbr);
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<List<SoMstr>> GetOpenOrders(string? custFilter = null)
    {
        var query = _db.SoMstr.Where(s => s.SoStatus == "O");
        if (custFilter != null) query = query.Where(s => s.SoCust == custFilter);
        return await query.OrderByDescending(s => s.SoEntdate).ToListAsync();
    }

    public async Task<CmMstr?> GetCustomer(string custCode)
        => await _db.CmMstr.FindAsync(custCode);

    public async Task<List<CmMstr>> SearchCustomers(string search, int maxResults = 50)
        => await _db.CmMstr
            .Where(c => c.CmCode.Contains(search) || c.CmName.Contains(search))
            .OrderBy(c => c.CmName)
            .Take(maxResults)
            .ToListAsync();

    public async Task<ServiceResult> AddCustomer(CmMstr cust)
    {
        try
        {
            if (await _db.CmMstr.AnyAsync(c => c.CmCode == cust.CmCode))
                return ServiceResult.Error("Customer already exists.");
            cust.CmCrtdate = DateTime.Today.ToString("yyyy-MM-dd");
            _db.CmMstr.Add(cust);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Customer added.", cust.CmCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding customer {Cust}", cust.CmCode);
            return ServiceResult.Error(ex.Message);
        }
    }

    public async Task<ServiceResult> UpdateCustomer(CmMstr cust)
    {
        try
        {
            _db.CmMstr.Update(cust);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Customer updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer {Cust}", cust.CmCode);
            return ServiceResult.Error(ex.Message);
        }
    }
}

// ── Authentication Service ─────────────────────────────────────────────────────

public interface IAuthService
{
    Task<(bool Success, string Token, string Message)> LoginAsync(string username, string password, string ip);
    Task LogoutAsync(string userId);
    Task<bool> ValidateTokenAsync(string token);
}

public class AuthService : IAuthService
{
    private readonly Microsoft.AspNetCore.Identity.UserManager<Models.Administration.ZaffreMeldUser> _userManager;
    private readonly Microsoft.AspNetCore.Identity.SignInManager<Models.Administration.ZaffreMeldUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        Microsoft.AspNetCore.Identity.UserManager<Models.Administration.ZaffreMeldUser> userManager,
        Microsoft.AspNetCore.Identity.SignInManager<Models.Administration.ZaffreMeldUser> signInManager,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _logger = logger;
    }

    public async Task<(bool Success, string Token, string Message)> LoginAsync(string username, string password, string ip)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user == null || !user.IsActive)
            return (false, string.Empty, "Invalid credentials.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed login for {User} from {IP}", username, ip);
            return (false, string.Empty, result.IsLockedOut ? "Account locked." : "Invalid credentials.");
        }

        user.LastLogin = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);
        _logger.LogInformation("User {User} logged in from {IP}", username, ip);
        return (true, token, "Login successful.");
    }

    public async Task LogoutAsync(string userId)
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User {UserId} logged out", userId);
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        // JWT validation is handled by the middleware; this is a placeholder for additional checks
        return Task.FromResult(!string.IsNullOrEmpty(token));
    }

    private string GenerateJwtToken(Models.Administration.ZaffreMeldUser user, IList<string> roles)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key missing");
        var secKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(secKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpiryHours"] ?? "8"));

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
            new(System.Security.Claims.ClaimTypes.Name, user.UserName ?? string.Empty),
            new("site", user.UserSite),
            new("usertype", user.UserType)
        };

        // Add a ClaimTypes.Role claim for every Identity role the user belongs to.
        // This is what [Authorize(Roles = "admin")] checks against.
        foreach (var role in roles)
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));

        // Also add usertype as a role if not already covered (admin users set via usertype field)
        if (!string.IsNullOrEmpty(user.UserType) && !roles.Contains(user.UserType))
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.UserType));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
