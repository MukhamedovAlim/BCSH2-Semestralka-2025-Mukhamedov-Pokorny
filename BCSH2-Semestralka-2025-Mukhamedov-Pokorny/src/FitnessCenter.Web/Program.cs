using FitnessCenter.Application.Interfaces;
using FitnessCenter.Application.Services;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Repozitáře
builder.Services.AddSingleton<IMembersRepository, OraceMemberRepository>();
builder.Services.AddScoped<ILessonRepository, OracleLessonsRepository>();
builder.Services.AddScoped<OracleLessonsRepository>();

// Aplikační služby
builder.Services.AddScoped<IMembersService, MembersService>();
builder.Services.AddScoped<ILessonsService, LessonsService>();
builder.Services.AddScoped<LessonsRepo>();
builder.Services.AddScoped<PaymentsReadRepo>();
builder.Services.AddScoped<ITrainersReadRepo, TrainersReadRepo>();

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

// Doporučení: https redirect klidně nechat i v dev
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// =========================
//   ROUTING / ENDPOINTS
// =========================

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
        // Address/Phone volitelně
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
