using FitnessCenter.Application.Interfaces;
using FitnessCenter.Application.Services;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Repozitáře (in-memory)
builder.Services.AddSingleton<IMembersRepository, InMemoryMembersRepository>();
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
app.UseAuthorization();

// Default – přesměruj na Login
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
