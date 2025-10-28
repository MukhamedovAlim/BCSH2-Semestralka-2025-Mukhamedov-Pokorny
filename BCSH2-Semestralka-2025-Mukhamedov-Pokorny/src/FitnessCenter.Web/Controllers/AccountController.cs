using System.Security.Claims;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IMembersService _members;

        public AccountController(IMembersService members)
        {
            _members = members;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        // POST: /Account/Login
        // (heslo teď neověřujeme – jen existence e-mailu; pro produkci přidej hash/ověření)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            // 1) najdi člena podle e-mailu
            var all = await _members.GetAllAsync();
            var member = all.FirstOrDefault(m =>
                string.Equals(m.Email, model.Email, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "Účet s tímto e-mailem neexistuje.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            // 2) zkusit zjistit, zda je trenér (podle existence v tabulce TRENERY)
            //    -> implementuj v IMembersService metodu IsTrainerEmailAsync(email)
            bool isTrainer = false;
            try
            {
                isTrainer = await _members.IsTrainerEmailAsync(member.Email);
            }
            catch
            {
                // pokud metodu zatím nemáš, dočasně necháme false
                // případně sem můžeš doplnit fallback na jiný repo
            }

            // 3) claimy (přidáme i ClenId kvůli Home/Index výpočtům permice)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString()),
                new Claim(ClaimTypes.Name, $"{member.FirstName} {member.LastName}".Trim()),
                new Claim(ClaimTypes.Email, member.Email),
                new Claim("ClenId", member.MemberId.ToString()),
                new Claim(ClaimTypes.Role, isTrainer ? "Trainer" : "Member")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            // 4) upřednostni validní lokální návratovou URL
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // 5) redirect podle role
            if (isTrainer)
                return RedirectToAction("Trainer", "Home");   // Dashboard trenéra

            return RedirectToAction("Index", "Home");          // Dashboard člena
        }

        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var all = await _members.GetAllAsync();
            if (all.Any(m => string.Equals(m.Email, model.Email, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.Email), "Tento e-mail už je zaregistrovaný.");
                return View(model);
            }

            var member = new Member
            {
                FirstName = model.FirstName?.Trim() ?? "",
                LastName = model.LastName?.Trim() ?? "",
                Email = model.Email?.Trim() ?? "",
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address!.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone!.Trim()
            };

            await _members.CreateAsync(member);

            TempData["JustRegistered"] = true;
            TempData["RegisterMsg"] = "Účet byl vytvořen. Přihlas se prosím.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/Logout
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/Denied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Denied()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Trainer"))
                    return RedirectToAction("Trainer", "Home");
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Admin", "Home");

                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction("Login", "Account");
        }
    }
}
