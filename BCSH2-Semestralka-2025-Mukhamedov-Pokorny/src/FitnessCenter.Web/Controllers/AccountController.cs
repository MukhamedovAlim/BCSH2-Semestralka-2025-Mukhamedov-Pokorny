using System.Security.Claims;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        // POST: /Account/Login  – pevné údaje: member/member, trener/trener
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            // --- 1) Ověření přihlašovacích údajů ---
            string? role = null;

            if (!string.IsNullOrWhiteSpace(model?.UserName) && model?.Password is not null)
            {
                if (model.UserName.Equals("trener", StringComparison.OrdinalIgnoreCase)
                    && model.Password == "trener")
                    role = "Trainer";
                else if (model.UserName.Equals("member", StringComparison.OrdinalIgnoreCase)
                    && model.Password == "member")
                    role = "Member";
            }

            if (role is null)
            {
                ModelState.AddModelError(string.Empty, "Neplatné přihlašovací údaje.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            // --- 2) Vytvoř cookie s claimy ---
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, model!.UserName),
        new Claim(ClaimTypes.Role, role)
    };
            var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(id);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            // --- 3) Preferuj returnUrl (když přesměrovávalo na login), jinak role-based redirect ---
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return role == "Trainer"
                ? RedirectToAction("Trainer", "Home")
                : RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register (demo)
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        // POST: /Account/Register (demo UX tok – nic neukládá)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult Register(RegisterViewModel model)
        {
            TempData["JustRegistered"] = true;
            TempData["RegisterMsg"] = "Účet byl úspěšně vytvořen. Teď se přihlas.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/Logout
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }
    }
}
