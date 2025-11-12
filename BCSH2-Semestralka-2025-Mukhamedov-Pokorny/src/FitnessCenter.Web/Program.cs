using System.Security.Claims;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Application.Services;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Oracle.ManagedDataAccess.Client;
using Microsoft.AspNetCore.Routing; // dej nahoru k usingům


var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// IHttpContextAccessor – potřebné pro LessonsService
builder.Services.AddHttpContextAccessor();

// =======================
//   Repozitáře (Scoped)
// =======================
builder.Services.AddScoped<IMembersRepository, OracleMemberRepository>();
builder.Services.AddScoped<ILessonRepository, OracleLessonsRepository>();
builder.Services.AddScoped<IAdminLogsRepository, AdminLogsRepository>();

// Read-only/Doplňkové repozitáře
builder.Services.AddScoped<OracleLessonsRepository>();
builder.Services.AddScoped<EquipmentRepository>();
builder.Services.AddScoped<PaymentsReadRepo>();
builder.Services.AddScoped<LessonsService>();
builder.Services.AddScoped<ITrainersReadRepo, TrainersReadRepo>();

// =======================
//   Aplikační služby
// =======================
builder.Services.AddScoped<IMembersService, MembersService>();
builder.Services.AddScoped<ILessonsService, LessonsService>();

// =======================
//   Session + Emulace uživatele
// =======================
builder.Services.AddSession(); // musí být přidáno, jinak nepůjde HttpContext.Session
builder.Services.AddScoped<IImpersonationService, ImpersonationService>();

// 🔐 Cookie autentizace
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Account/Denied";
        opt.LogoutPath = "/Account/Logout";
        opt.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Error handling + HSTS v produkci
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();       // musí být aktivováno před autentizací
app.UseAuthentication();
app.UseAuthorization();

// =========================
//   ROUTING / ENDPOINTS
// =========================

// Dočasný výpis zaregistrovaných endpointů (pro diagnostiku 405)
app.MapGet("/_routes", (IEnumerable<EndpointDataSource> sources) =>
{
    var lines = sources
        .SelectMany(s => s.Endpoints)
        .OfType<RouteEndpoint>()
        .Select(e =>
        {
            var http = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                       ?? new[] { "ALL" };
            return $"{e.RoutePattern.RawText}   [{string.Join(",", http)}]";
        });

    return Results.Text(string.Join(Environment.NewLine, lines), "text/plain");
});

// přesný kořen /Members -> Index
app.MapControllerRoute(
    name: "members_root",
    pattern: "Members",
    defaults: new { controller = "Members", action = "Index" });

// obecné members akce
app.MapControllerRoute(
    name: "members",
    pattern: "Members/{action=Index}/{id?}",
    defaults: new { controller = "Members" });

// (až potom) default
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Default – nejdřív login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Root "/" → Login
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});

// 🔧 DB smoke-test: navštiv /dbtest a čekej "DB status: OK"
app.MapGet("/dbtest", async () =>
{
    try
    {
        using OracleConnection conn = await DatabaseManager.GetOpenConnectionAsync();
        using var cmd = new OracleCommand("SELECT 'OK' FROM DUAL", conn);
        var res = (string)await cmd.ExecuteScalarAsync();
        return Results.Text($"DB status: {res}", "text/plain");
    }
    catch (OracleException ox)
    {
        return Results.Text($"Oracle ERROR {ox.Number}: {ox.Message}", "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"ERROR: {ex.Message}", "text/plain");
    }
});

// Test: vlož člena (volá service/repo)
app.MapGet("/members/test-insert", async (IMembersService svc) =>
{
    var id = await svc.CreateAsync(new Member
    {
        FirstName = "Test",
        LastName = "User",
        Email = $"test{DateTime.UtcNow.Ticks}@example.com"
    });
    return Results.Text($"Inserted member id: {id}");
});

// Test: výpis členů
app.MapGet("/members/test-list", async (IMembersService svc) =>
{
    var all = await svc.GetAllAsync();
    var lines = all.Select(m => $"{m.MemberId}: {m.FirstName} {m.LastName} <{m.Email}>");
    return Results.Text(string.Join("\n", lines));
});

app.Run();
