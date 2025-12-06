using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Web.Models.Admin;
using FitnessCenter.Web.Models.AdminDashboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Admin")]
[Route("Admin/Dashboard")]
public sealed class AdminDashboardController : Controller
{
    private readonly DashboardRepository _repo;

    public AdminDashboardController(DashboardRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Oprava: Použijte existující metodu GetDailyRevenueAsync a explicitně uveďte typy proměnných
        var from = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        (List<string> dny, List<decimal> trzby) = await _repo.GetDailyRevenueAsync(from, to);

        var vm = new AdminDashboardViewModel
        {
            Mesice = dny, // Pokud chcete zobrazit dny, jinak upravte ViewModel
            Trzby = trzby
        };

        return View(vm);
    }
}
