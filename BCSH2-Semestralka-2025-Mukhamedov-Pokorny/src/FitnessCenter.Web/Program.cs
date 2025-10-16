using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using FitnessCenter.Application.Interfaces;
using FitnessCenter.Application.Services;
using FitnessCenter.Infrastructure.Repositories;

// top-level Program:
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// In-memory repo (později přepneme na SQL/Dapper)
builder.Services.AddSingleton<IMembersRepository, InMemoryMembersRepository>();
builder.Services.AddScoped<IMembersService, MembersService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// (auth zatím nepoužíváme, ale necháme připravené)
app.UseAuthorization();

// Default route na Account/Login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// (volitelné – jistota, kdyby VS/launchUrl nutil root "/")
app.MapGet("/", ctx => {
    ctx.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});

app.Run();
