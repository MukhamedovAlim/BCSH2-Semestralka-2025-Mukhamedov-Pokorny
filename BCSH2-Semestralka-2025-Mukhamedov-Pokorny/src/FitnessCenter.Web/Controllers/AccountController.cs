using System.Security.Claims;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;           // DatabaseManager
using FitnessCenter.Web.Infrastructure.Security;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        // GET: /Account/Login
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
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // 1) zjistíme aktuálního člena z Claims
            int memberId = User.GetRequiredCurrentMemberId();

            var member = await _members.GetByIdAsync(memberId);
            if (member == null)
            {
                TempData["Err"] = "Uživatel nenalezen.";
                return RedirectToAction("Index", "Home");
            }

            // 2) ověříme aktuální heslo
            var verify = _hasher.VerifyHashedPassword(member, member.PasswordHash ?? "", vm.CurrentPassword);
            if (verify == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(nameof(vm.CurrentPassword), "Aktuální heslo je špatně.");
                return View(vm);
            }

            // 3) vytvoříme nový hash
            var newHash = _hasher.HashPassword(member, vm.NewPassword);

            // 4) uložíme přes MembersService → repo → UPDATE v DB
            await _members.ChangePasswordAsync(memberId, newHash);

            // 5) označíme v tomhle prohlížeči, že heslo už bylo změněno
            Response.Cookies.Append("pwd_changed", "1", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                SameSite = SameSiteMode.Strict
            });

            TempData["Ok"] = "Heslo bylo úspěšně změněno.";
            return RedirectToAction("Profile", "Members");
        }

        // ========================
        //        LOGIN (POST)
        // ========================
        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // 1) Ruční kontrola vstupu – žádné ModelState.IsValid
            if (string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                model.ErrorMessage = "Vyplň prosím e-mail i heslo.";
                return View(model);
            }

            // 2) Najdi člena podle e-mailu
            var all = await _members.GetAllAsync();

            var normalizedEmail = (model.Email ?? string.Empty).Trim().ToLowerInvariant();

            var member = all.FirstOrDefault(m =>
                ((m.Email ?? string.Empty).Trim().ToLowerInvariant()) == normalizedEmail);

            if (member == null)
            {
                model.ErrorMessage = "Účet s tímto e-mailem neexistuje.";
                return View(model);
            }

            var email = (member.Email ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(member.PasswordHash))
            {
                model.ErrorMessage = "Tomuto účtu chybí nastavené heslo. Kontaktuj správce.";
                return View(model);
            }

            var verifyResult = _hasher.VerifyHashedPassword(member, member.PasswordHash, model.Password ?? string.Empty);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                model.ErrorMessage = "Neplatné heslo.";
                return View(model);
            }

            // 3) Role: Admin / Trainer
            bool isAdmin = false;
            using (var con = await DatabaseManager.GetOpenConnectionAsync())
            using (var cmd = new OracleCommand(
                       "SELECT 1 FROM ADMINI WHERE LOWER(EMAIL)=LOWER(:em) FETCH FIRST 1 ROWS ONLY",
                       (OracleConnection)con))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("em", OracleDbType.Varchar2).Value = email;
                var obj = await cmd.ExecuteScalarAsync();
                isAdmin = obj != null;
            }

            bool isTrainer = false;
            int? trainerId = null;
            try
            {
                isTrainer = await _members.IsTrainerEmailAsync(email);
                if (isTrainer)
                {
                    var tid = await _members.GetTrainerIdByEmailAsync(email);
                    if (tid != null && tid.Value > 0)
                        trainerId = tid.Value;
                }
            }
            catch
            {
                // necháme isTrainer = false / trainerId = null
            }

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

            if (isTrainer) claims.Add(new Claim(ClaimTypes.Role, "Trainer"));
            if (trainerId.HasValue) claims.Add(new Claim("TrainerId", trainerId.Value.ToString()));
            if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

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

            // 🔸 Pokud nemá cookie, zobrazíme doporučení na změnu hesla
            if (!Request.Cookies.ContainsKey("pwd_changed"))
            {
                TempData["PwdNotice"] = "Používáš vygenerované heslo. Doporučujeme ho změnit.";
            }

            // 5) Redirecty
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (isAdmin) return RedirectToAction("Admin", "Home");
            if (isTrainer) return RedirectToAction("Trainer", "Home");
            return RedirectToAction("Index", "Home");
        }

        // ========================
        //        REGISTER
        // ========================
        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Register()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();   // naplníme dropdown
            return View(new RegisterViewModel());
        }

        // POST: /Account/Register
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
        // GET: /Account/Logout
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ========================
        //         DENIED
        // ========================
        // GET: /Account/Denied
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
