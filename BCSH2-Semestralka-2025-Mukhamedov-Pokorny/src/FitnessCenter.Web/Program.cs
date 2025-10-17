using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.Cookies;

using FitnessCenter.Application.Interfaces;
using FitnessCenter.Application.Services;
using FitnessCenter.Infrastructure.Repositories;

// top-level Program:
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// In-memory repo (později přepneme na SQL/Dapper)
builder.Services.AddSingleton<IMembersRepository, InMemoryMembersRepository>();
builder.Services.AddScoped<IMembersService, MembersService>();

// 🔐 Cookie autentizace (Member role zatím řeš přes claims v AccountControlleru)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Account/Denied";
        opt.LogoutPath = "/Account/Logout";
        opt.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(); // zatím bez speciálních polic

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();   // ⬅️ přesunuto dovnitř podmínky
}

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();


// Default route – start na login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Jistota pro root "/"
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});

app.Run();
