using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
        => RedirectToAction("Index", "Members"); // dočasně pošleme na seznam členů
}
