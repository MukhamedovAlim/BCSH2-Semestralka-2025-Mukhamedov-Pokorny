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

        // POST: /Account/Login  – e-mail + heslo (heslo teď neověřujeme, jen existence e-mailu v DB)
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

            // Najdi člena podle e-mailu
            var all = await _members.GetAllAsync();
            var member = all.FirstOrDefault(m =>
                string.Equals(m.Email, model.Email, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "Účet s tímto e-mailem neexistuje.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            // Vytvoř cookie s claimy (role Member)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString()),
                new Claim(ClaimTypes.Name, $"{member.FirstName} {member.LastName}".Trim()),
                new Claim(ClaimTypes.Email, member.Email),
                new Claim(ClaimTypes.Role, "Member")
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

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home"); // dashboard člena
        }

        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        // POST: /Account/Register – uloží do CLENOVE (ID ze sekvence, SYSDATE pro narození, FK fitness se vybere v repo)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // kontrola unikátního e-mailu (přátelská hláška)
            var all = await _members.GetAllAsync();
            if (all.Any(m => string.Equals(m.Email, model.Email, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.Email), "Tento e-mail už je zaregistrovaný.");
                return View(model);
            }

            // vytvoř člena (repo doplní ID ze sekvence, SYSDATE, FK na fitness centrum)
            var member = new Member
            {
                FirstName = model.FirstName?.Trim() ?? "",
                LastName = model.LastName?.Trim() ?? "",
                Email = model.Email?.Trim() ?? "",
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address!.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone!.Trim()
            };

            await _members.CreateAsync(member);

            // po úspěchu jen přesměruj na Login a ukaž zprávu
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

        // GET: /Account/Denied  → jen přesměruje
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Denied()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                string? target =
                    User.IsInRole("Admin") ? Url.Action("Admin", "Home") :
                    User.IsInRole("Trainer") ? Url.Action("Trainer", "Home") :
                                               Url.Action("Index", "Home");
                return Redirect(target ?? Url.Action("Login", "Account")!);
            }
            return RedirectToAction("Login", "Account");
        }
    }
}
