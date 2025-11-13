using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Infrastructure.Security;

using FitnessCenter.Infrastructure.Repositories;   // PaymentsReadRepo
using FitnessCenter.Application.Interfaces;      // ILessonsService, IMembersService
using FitnessCenter.Infrastructure.Persistence;  // DatabaseManager
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly PaymentsReadRepo _payments;
        private readonly ILessonsService _lessons;
        private readonly IMembersService _members;

        public HomeController(
            PaymentsReadRepo payments,
            ILessonsService lessons,
            IMembersService members)
        {
            _payments = payments;
            _lessons = lessons;
            _members = members;
        }

        // ===== Member dashboard =====
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Home";

            // 🔑 univerzální – funguje pro normálního člena i emulaci
            int clenId = User.GetRequiredCurrentMemberId();

            var ms = await _payments.GetMembershipAsync(clenId);
            ViewBag.permActive = ms.Active;
            ViewBag.permType = ms.TypeName ?? "-";
            ViewBag.permPrice = "-";

            if (ms.Active && ms.From.HasValue && ms.To.HasValue)
            {
                var total = (ms.To.Value.Date - ms.From.Value.Date).TotalDays;
                var used = (DateTime.Today - ms.From.Value.Date).TotalDays;
                var usedPct = total <= 0 ? 100 : Math.Clamp((int)Math.Round(100 * used / total), 0, 100);

                ViewBag.permFrom = ms.From;
                ViewBag.permTo = ms.To;
                ViewBag.daysLeft = ms.DaysLeft;
                ViewBag.usedPct = usedPct;
            }
            else
            {
                ViewBag.permFrom = (DateTime?)null;
                ViewBag.permTo = (DateTime?)null;
                ViewBag.daysLeft = 0;
                ViewBag.usedPct = 0;
            }

            return View();
        }

        // ===== Trainer dashboard – dnešní lekce =====
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> Trainer()
        {
            ViewBag.Active = "HomeTrainer";

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // trainerId podle e-mailu přihlášeného uživatele
            var email = User.FindFirstValue(ClaimTypes.Email);
            var trainerId = string.IsNullOrWhiteSpace(email)
                ? (int?)null
                : await _members.GetTrainerIdByEmailAsync(email);

            if (trainerId is null)
            {
                TempData["Err"] = "K účtu není přiřazen žádný trenér.";
                ViewBag.Today = today;
                ViewBag.LessonsToday = Array.Empty<object>();
                return View();
            }

            var all = await _lessons.GetForTrainerAsync(trainerId.Value);

            var todays = all
                .Where(l => l.Zacatek >= today && l.Zacatek < tomorrow)
                .OrderBy(l => l.Zacatek)
                .Select(l => new
                {
                    Id = l.Id,
                    Time = l.Zacatek.ToString("HH:mm"),
                    Name = l.Nazev,
                    Room = string.IsNullOrWhiteSpace(l.Mistnost) ? "—" : l.Mistnost,
                    // zatím jen kapacita; můžeš později doplnit „obsazeno/volno“
                    Slots = l.Kapacita.ToString()
                })
                .ToList();

            ViewBag.Today = today;
            ViewBag.LessonsToday = todays;

            return View();
        }

        // ===== Admin dashboard – statistiky =====
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            ViewBag.Active = "Admin";
            ViewBag.Today = DateTime.Today;
            ViewBag.HideMainNav = true;

            int members = 0, trainers = 0, lessonsToday = 0, pendingPayments = 0, logCount = 0, equipmentCount = 0;

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                var oc = (OracleConnection)con;

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM CLENOVE", oc))
                    members = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM TRENERI", oc))
                    trainers = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand(
                    "SELECT COUNT(*) FROM LEKCE WHERE TRUNC(DATUMLEKCE) = TRUNC(SYSDATE)", oc))
                    lessonsToday = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand(
                    "SELECT COUNT(*) FROM PLATBY WHERE STAVPLATBY_IDSTAVPLATBY = 1", oc))
                    pendingPayments = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM LOG_OPERACE", oc))
                    logCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM VYBAVENI", oc))
                    equipmentCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst statistiky: " + ex.Message;
            }

            ViewBag.MembersCount = members;
            ViewBag.TrainersCount = trainers;
            ViewBag.LessonsToday = lessonsToday;
            ViewBag.PendingPayments = pendingPayments;
            ViewBag.AdminMessagesCnt = 0;
            ViewBag.LogCount = logCount;
            ViewBag.EquipmentCount = equipmentCount;

            return View();
        }
    }
}