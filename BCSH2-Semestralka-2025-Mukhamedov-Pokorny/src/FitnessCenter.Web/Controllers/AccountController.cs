using FitnessCenter.Application.Interfaces;
using FitnessCenter.Infrastructure.Persistence;           // DatabaseManager
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;                    // OracleCommand/Types
using System.Security.Claims;


namespace FitnessCenter.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IMembersService _members;

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

        // POST: /Account/Login
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

            // 1) Najdi člena podle e-mailu (tvoje stávající logika)
            var all = await _members.GetAllAsync();
            var member = all.FirstOrDefault(m =>
                string.Equals(m.Email, model.Email, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "Účet s tímto e-mailem neexistuje.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var email = member.Email.Trim();

            // 2) Zjisti role: Admin přes tabulku ADMINI, Trainer přes službu
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
                    // Pokud to tvoje služba umí, získáme i ID trenéra pro claim
                    // (když ne, try/catch to spolkne a claim se jen nepřidá)
                    var tid = await _members.GetTrainerIdByEmailAsync(email);
                    if (tid != null && tid.Value > 0) trainerId = tid.Value;
                }
            }
            catch
            {
                // necháme isTrainer=false / trainerId null, a pokračujeme
            }

            // 3) Claims
            var memberIdStr = member.MemberId.ToString();

            var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, memberIdStr),

    // sjednocení s emulací – ať je to všude stejné
    new Claim("MemberId", memberIdStr),
    new Claim("UserId", memberIdStr),

    // kvůli starším částem kódu klidně necháme i ClenId
    new Claim("ClenId", memberIdStr),

    new Claim(ClaimTypes.Name, $"{member.FirstName} {member.LastName}".Trim()),
    new Claim(ClaimTypes.Email, email),
    new Claim(ClaimTypes.Role, "Member") // Member vždy
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

            // 4) Preferuj bezpečný návrat (pokud je)
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // 5) Redirect podle role – Admin > Trainer > Member
            if (isAdmin) return RedirectToAction("Admin", "Home");
            if (isTrainer) return RedirectToAction("Trainer", "Home");
            return RedirectToAction("Index", "Home");
        }

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

            var member = new FitnessCenter.Domain.Entities.Member
            {
                FirstName = model.FirstName?.Trim() ?? "",
                LastName = model.LastName?.Trim() ?? "",
                Email = model.Email?.Trim() ?? "",
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                BirthDate = model.BirthDate!.Value,
                FitnessCenterId = model.FitnessCenterId
            };

            try
            {
                await _members.CreateViaProcedureAsync(member); // PR_CLEN_CREATE
                TempData["JustRegistered"] = true;
                TempData["RegisterMsg"] = "Účet byl vytvořen. Přihlas se prosím.";
                return RedirectToAction(nameof(Login));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message); // duplicitní email/telefon apod.
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }
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
                if (User.IsInRole("Admin")) return RedirectToAction("Admin", "Home");
                if (User.IsInRole("Trainer")) return RedirectToAction("Trainer", "Home");
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction("Login", "Account");
        }
    }
}
