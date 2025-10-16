using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            // DEMO ovìøení – jen pro vizuál:
            bool ok = model.UserName.Equals("demo", StringComparison.OrdinalIgnoreCase)
                      && model.Password == "demo123";

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Neplatné pøihlašovací údaje.");
                return View(model);
            }

            TempData["Toast"] = "Pøihlášení probìhlo (demo).";
            return RedirectToAction("Index", "Home"); // mùžeš zmìnit tøeba na Members/Index
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Zatím nic neukládáme – jen UX tok:
            TempData["Toast"] = "Úèet vytvoøen (demo). Teï se pøihlas.";
            return RedirectToAction(nameof(Login));
        }
    }
}
