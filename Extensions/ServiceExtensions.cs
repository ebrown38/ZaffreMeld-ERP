using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Web.Extensions;

/// <summary>
/// Extension methods for registering all ZaffreMeld services with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZaffreMeldServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core app service
        services.AddScoped<IZaffreMeldAppService, ZaffreMeldAppService>();

        // Domain services
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
