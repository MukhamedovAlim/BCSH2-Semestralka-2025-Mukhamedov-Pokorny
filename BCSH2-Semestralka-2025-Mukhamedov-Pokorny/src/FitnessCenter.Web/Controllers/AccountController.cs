using System.Security.Claims;
using System.Linq;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;           // DatabaseManager
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IMembersService _members;
        private readonly PasswordHasher<Member> _hasher = new();

        // TODO: uprav si na reálný admin e-mail
        private const string AdminEmail = "pokdavi@seznam.cz";

        public AccountController(IMembersService members)
        {
            _members = members;
        }

        private static async Task<List<SelectListItem>> LoadFitnessForSelectAsync()
        {
            var items = new List<SelectListItem>();
            using var con = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                "SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev",
                (OracleConnection)con);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                items.Add(new SelectListItem
                {
                    Value = rd.GetInt32(0).ToString(),
                    Text = rd.GetString(1)
                });
            }
            return items;
        }

        // ========================
        //        LOGIN (GET)
        // ========================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // Pokud už je uživatel přihlášený, pošli ho rovnou na jeho dashboard
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Admin", "Home");
                if (User.IsInRole("Trainer")) return RedirectToAction("Trainer", "Home");
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        // ============================
        //     ZMĚNA HESLA (GET/POST)
        // ============================
        [Authorize]
        [HttpGet("/Account/ChangePassword")]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [Authorize]
        [HttpPost("/Account/ChangePassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            // 1) základní modelová validace (Required, Compare, ...)
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ",
                    ModelState.Values
                              .SelectMany(v => v.Errors)
                              .Select(e => e.ErrorMessage));

                TempData["Err"] = string.IsNullOrWhiteSpace(errors)
                    ? "Formulář není validní."
                    : "Formulář není validní: " + errors;

                return View(model);
            }

            // 2) načteme ID uživatele z claims
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var memberId))
            {
                TempData["Err"] = "Nepodařilo se zjistit ID uživatele z přihlášení.";
                await HttpContext.SignOutAsync();
                return RedirectToAction("Login");
            }

            // 3) načteme člena z databáze
            var member = await _members.GetByIdAsync(memberId);
            if (member == null)
            {
                TempData["Err"] = "Uživatel v databázi neexistuje.";
                await HttpContext.SignOutAsync();
                return RedirectToAction("Login");
            }

            // 4) ověření aktuálního hesla
            var verify = _hasher.VerifyHashedPassword(member, member.PasswordHash, model.CurrentPassword);
            if (verify != PasswordVerificationResult.Success)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Aktuální heslo není správně.");
                TempData["Err"] = "Aktuální heslo není správně.";
                return View(model);
            }

            // 5) kontrola nového hesla
            if (string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                TempData["Err"] = "Nové heslo a potvrzení nesmí být prázdné.";
                return View(model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Nová hesla se neshodují.");
                TempData["Err"] = "Nová hesla se neshodují.";
                return View(model);
            }

            if (model.NewPassword == model.CurrentPassword)
            {
                TempData["Err"] = "Nové heslo nesmí být stejné jako staré.";
                return View(model);
            }

            // 6) změna hesla v DB – procedura zároveň nastaví MUSI_ZMENIT_HESLO = 0
            var newHash = _hasher.HashPassword(member, model.NewPassword);
            await _members.ChangePasswordAsync(memberId, newHash);

            // 7) znovu přihlásíme uživatele bez MustChangePassword claimu
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString()),
        new Claim("MemberId",  member.MemberId.ToString()),
        new Claim("UserId",    member.MemberId.ToString()),
        new Claim("ClenId",    member.MemberId.ToString()),
        new Claim(ClaimTypes.Name, $"{member.FirstName} {member.LastName}".Trim()),
        new Claim(ClaimTypes.Email, member.Email),
        new Claim(ClaimTypes.Role, "Member")
    };

            if (User.IsInRole("Trainer"))
                claims.Add(new Claim(ClaimTypes.Role, "Trainer"));

            var trainerIdClaim = User.FindFirst("TrainerId")?.Value;
            if (!string.IsNullOrEmpty(trainerIdClaim))
                claims.Add(new Claim("TrainerId", trainerIdClaim));

            if (User.IsInRole("Admin"))
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

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

            TempData["Ok"] = "Heslo bylo úspěšně změněno.";
            return RedirectToAction("Index", "Home");
        }





        // ========================
        //        LOGIN (POST)
        // ========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "Zadej e-mail a heslo.");
                return View(model);
            }

            // hnus, ale používáš to tak – vybereš všechny a najdeš
            var all = await _members.GetAllAsync();
            var member = all.FirstOrDefault(c =>
                c.Email != null && c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                ModelState.AddModelError("", "Neplatný e-mail nebo heslo.");
                return View(model);
            }

            var result = _hasher.VerifyHashedPassword(member, member.PasswordHash, model.Password);
            if (result != PasswordVerificationResult.Success)
            {
                ModelState.AddModelError("", "Neplatný e-mail nebo heslo.");
                return View(model);
            }

            // ZJISTIT ROLE (trainer / admin)
            bool isTrainer = await _members.IsTrainerEmailAsync(email);
            int? trainerId = null;
            if (isTrainer)
                trainerId = await _members.GetTrainerIdByEmailAsync(email);

            // 🔥 Admin stejně jako dřív – podle e-mailu
            bool isAdmin = email.Equals(AdminEmail, StringComparison.OrdinalIgnoreCase);

            // 4) Claims
            var memberIdStr = member.MemberId.ToString();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, memberIdStr),

                new Claim("MemberId", memberIdStr),
                new Claim("UserId", memberIdStr),
                new Claim("ClenId", memberIdStr),

                new Claim(ClaimTypes.Name, $"{member.FirstName} {member.LastName}".Trim()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, "Member")
            };

            if (isTrainer)
                claims.Add(new Claim(ClaimTypes.Role, "Trainer"));

            if (trainerId.HasValue)
                claims.Add(new Claim("TrainerId", trainerId.Value.ToString()));

            if (isAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            // 🔥 DŮLEŽITÉ – claim MustChangePassword
            if (member.MustChangePassword)
            {
                claims.Add(new Claim("MustChangePassword", "true"));
            }

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

            // Pokud musí změnit heslo → přesměruj na ChangePassword
            if (member.MustChangePassword)
            {
                return RedirectToAction("ChangePassword", "Account");
            }

            // jinak na Home / nebo původní returnUrl
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // Admin → admin dashboard
            if (isAdmin)
                return RedirectToAction("Admin", "Home");

            // Trenér → trainer dashboard
            if (isTrainer)
                return RedirectToAction("Trainer", "Home");

            // běžný člen → Home
            return RedirectToAction("Index", "Home");
        }

        // ========================
        //        REGISTER
        // ========================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Register()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();   // naplníme dropdown
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(); // znovu při chybě
                return View(model);
            }

            // duplicitní e-mail – rychlá kontrola
            var all = await _members.GetAllAsync();
            if (all.Any(m => string.Equals(m.Email, model.Email, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.Email), "Tento e-mail už je zaregistrovaný.");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }

            // pojistka: zvolený FitnessCenter existuje
            using (var con = await DatabaseManager.GetOpenConnectionAsync())
            using (var chk = new OracleCommand("SELECT COUNT(*) FROM fitnesscentra WHERE idfitness=:id", (OracleConnection)con))
            {
                chk.BindByName = true;
                chk.Parameters.Add("id", model.FitnessCenterId);
                var exists = Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    ModelState.AddModelError(nameof(model.FitnessCenterId), "Zvolené fitness centrum neexistuje.");
                    ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                    return View(model);
                }
            }

            var tempMember = new Member();
            var passwordHash = _hasher.HashPassword(tempMember, model.Password);

            System.Diagnostics.Debug.WriteLine("EMAIL: '" + model.Email + "'");
            System.Diagnostics.Debug.WriteLine("HESLO: '" + model.Password + "'");

            var member = new Member
            {
                FirstName = model.FirstName?.Trim() ?? "",
                LastName = model.LastName?.Trim() ?? "",
                Email = model.Email?.Trim() ?? "",
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                BirthDate = model.BirthDate!.Value,
                FitnessCenterId = model.FitnessCenterId,
                PasswordHash = passwordHash
            };

            try
            {
                await _members.CreateViaProcedureAsync(member); // PR_CLEN_CREATE musí nově brát i PASSWORD_HASH
                TempData["JustRegistered"] = true;
                TempData["RegisterMsg"] = "Účet byl vytvořen. Přihlas se prosím.";
                return RedirectToAction(nameof(Login));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }
        }

        // ========================
        //         LOGOUT
        // ========================
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ========================
        //         DENIED
        // ========================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Denied()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Admin", "Home");
                if (User.IsInRole("Trainer")) return RedirectToAction("Trainer", "Home");
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction("Login", "Account");
        }
    }
}
