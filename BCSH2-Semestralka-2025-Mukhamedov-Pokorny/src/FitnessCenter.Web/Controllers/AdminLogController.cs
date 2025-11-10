using FitnessCenter.Application.Interfaces;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Admin")]
public sealed class AdminLogsController : Controller
{
    private readonly IAdminLogsRepository repo;
    public AdminLogsController(IAdminLogsRepository repo) => this.repo = repo;

    public async Task<IActionResult> Index(int top = 200)
        => View(await repo.GetLogsAsync(top));
}
