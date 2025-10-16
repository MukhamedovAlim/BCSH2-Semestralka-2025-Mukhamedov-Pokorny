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

            // DEMO ov��en� � jen pro vizu�l:
            bool ok = model.UserName.Equals("demo", StringComparison.OrdinalIgnoreCase)
                      && model.Password == "demo123";

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Neplatn� p�ihla�ovac� �daje.");
                return View(model);
            }

            TempData["Toast"] = "P�ihl�en� prob�hlo (demo).";
            return RedirectToAction("Index", "Home"); // m��e� zm�nit t�eba na Members/Index
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Zat�m nic neukl�d�me � jen UX tok:
            TempData["Toast"] = "��et vytvo�en (demo). Te� se p�ihlas.";
            return RedirectToAction(nameof(Login));
        }
    }
}
