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

// Repozitáře (in-memory)
builder.Services.AddSingleton<IMembersRepository, OracleMembersRepository>();
builder.Services.AddSingleton<ILessonRepository, InMemoryLessonsRepository>();

// Aplikační služby
builder.Services.AddScoped<IMembersService, MembersService>();
builder.Services.AddScoped<ILessonsService, LessonsService>();

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
// DEV: automaticky přihlásí Admina na localhostu
if (app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        bool isLocal =
            ctx.Request.Host.Host is "localhost" or "127.0.0.1" or "::1";
        bool skip =
            ctx.Request.Path.StartsWithSegments("/Account/Login") ||
            ctx.Request.Path.StartsWithSegments("/Account/Logout") ||
            ctx.Request.Query.ContainsKey("noautologin"); // možnost vypnout: ?noautologin=1

        if (isLocal && !skip && !(ctx.User?.Identity?.IsAuthenticated ?? false))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  "Dev Admin"),
                new(ClaimTypes.Email, "dev.admin@local"),
                new(ClaimTypes.Role,  "Admin")
            };
            var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(id);

            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });
        }

        await next();
    });
}
app.UseAuthorization();

// Default – přesměruj na Login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

// Jistota pro root "/"
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/Admin");
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

// vlož test člena
app.MapGet("/members/test-insert", async (IMembersService svc) =>
{
    var id = await svc.CreateAsync(new Member
    {
        FirstName = "Test",
        LastName = "User",
        Email = $"test{DateTime.UtcNow.Ticks}@example.com"
        // Address/Phone necháme NULL
    });
    return Results.Text($"Inserted member id: {id}");
});

// vypiš členy
app.MapGet("/members/test-list", async (IMembersService svc) =>
{
    var all = await svc.GetAllAsync();
    var lines = all.Select(m => $"{m.MemberId}: {m.FirstName} {m.LastName} <{m.Email}>");
    return Results.Text(string.Join("\n", lines));
});


app.Run();
