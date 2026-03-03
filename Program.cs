using ZaffreMeld.Web.Data;
using ZaffreMeld.Web.Extensions;
using ZaffreMeld.Web.Middleware;
using ZaffreMeld.Web.Models.Administration;
using ZaffreMeld.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

// Bootstrap logger so startup messages are visible immediately
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("ZaffreMeld ERP starting...");

try
{
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// ─── Serilog ─────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ─── Database ─────────────────────────────────────────────────────────────────
var dbType = builder.Configuration["ZaffreMeld:DatabaseType"] ?? "sqlite";
switch (dbType.ToLower())
{
    case "mysql":
        builder.Services.AddDbContext<ZaffreMeldDbContext>(options =>
            options.UseMySQL(
                builder.Configuration.GetConnectionString("DefaultConnection")!));
        break;
    case "sqlserver":
        builder.Services.AddDbContext<ZaffreMeldDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
        break;
    default: // sqlite
        builder.Services.AddDbContext<ZaffreMeldDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection")));
        break;
}

// ─── Identity ─────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ZaffreMeldUser, ZaffreMeldRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ZaffreMeldDbContext>()
.AddDefaultTokenProviders();

// ─── JWT Auth ─────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        // Explicitly map role claims so [Authorize(Roles="admin")] works correctly
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        NameClaimType  = System.Security.Claims.ClaimTypes.Name
    };
    // Support cookie-based token for MVC views
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["bs_token"];
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ─── MVC + Razor Views ────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson();

builder.Services.AddRazorPages();

// ─── Session ──────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "bs_session";
});

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.AddZaffreMeldServices(builder.Configuration);

// ─── Swagger / API Docs ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ZaffreMeld ERP API",
        Version = "v1",
        Description = "ZaffreMeld ERP REST API — converted from Java Servlet to ASP.NET Core"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("ZaffreMeldPolicy", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// ─── HttpContextAccessor ──────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZaffreMeld API v1");
        c.RoutePrefix = "api-docs";
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("ZaffreMeldPolicy");
app.UseSerilogRequestLogging();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Custom request logging middleware (mirrors Java bslog)
app.UseZaffreMeldRequestLogging();

// ─── Routes ───────────────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ─── Database initialization ──────────────────────────────────────────────────
Log.Information("Initializing database...");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ZaffreMeldDbContext>();
    await db.Database.EnsureCreatedAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ZaffreMeldUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ZaffreMeldRole>>();
    await ZaffreMeldDbInitializer.SeedAsync(db, userManager, roleManager);
}
Log.Information("Database ready. Starting web server on http://localhost:5000 ...");
Log.Information("Login at http://localhost:5000/login  |  API docs at http://localhost:5000/api-docs");

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "ZaffreMeld ERP failed to start");
}
finally
{
    Log.CloseAndFlush();
}
