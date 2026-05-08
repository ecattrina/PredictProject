using ForecastApp1.Data;
using ForecastApp1.Services;
using ForecastApp1.Services.Auth;
using ForecastApp1.Services.Calculation;
using ForecastApp1.Services.Calendar;
using ForecastApp1.Services.Import;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connSetting = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=App_Data/forecast.db";
string sqliteConnection;
if (connSetting.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var path = connSetting["Data Source=".Length..].Trim();
    var fullPath = Path.IsPathRooted(path)
        ? path
        : Path.Combine(builder.Environment.ContentRootPath, path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    sqliteConnection = $"Data Source={fullPath}";
}
else
{
    sqliteConnection = connSetting;
}

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlite(sqliteConnection));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IImportFileStorage, LocalImportFileStorage>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<IImportValidationService, ImportValidationService>();
builder.Services.AddScoped<IImportCommitService, ImportCommitService>();
builder.Services.AddScoped<IImportRollbackService, ImportRollbackService>();
builder.Services.AddScoped<IBusinessCalendarService, BusinessCalendarService>();
builder.Services.AddScoped<IPaymentCalculationService, PaymentCalculationService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "forecast_auth";
        o.LoginPath = "/login.html";
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllersWithViews();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    try
    {
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not ensure SQLite database (check file path and permissions).");
    }
    try
    {
        await DbSeeder.SeedAsync(db, pwd);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Seeding skipped (database may be unavailable or already seeded).");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/js/api.js", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            ctx.Context.Response.Headers["Pragma"] = "no-cache";
            ctx.Context.Response.Headers["Expires"] = "0";
        }
    }
});
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");
app.Run();
