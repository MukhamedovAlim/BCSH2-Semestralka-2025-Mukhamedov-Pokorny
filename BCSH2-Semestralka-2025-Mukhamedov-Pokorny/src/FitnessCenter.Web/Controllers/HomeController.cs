using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Infrastructure.Repositories; // PaymentsReadRepo

namespace FitnessCenter.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly PaymentsReadRepo _payments;

        public HomeController(PaymentsReadRepo payments)
        {
            _payments = payments;
        }

        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Home";

            int clenId = 0;
            int.TryParse(User.FindFirst("ClenId")?.Value, out clenId);
            if (clenId == 0)
            {
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var resolved = await _payments.GetMemberIdByEmailAsync(email);
                    clenId = resolved ?? 0;
                }
            }

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

        // Dashboard pro trenéra – ponecháno
        [Authorize(Roles = "Trainer")]
        public IActionResult Trainer()
        {
            ViewBag.Active = "HomeTrainer";
            ViewBag.Today = DateTime.Today;
            return View();
        }

        // Dashboard pro admina – ponecháno
        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            ViewBag.Active = "HomeAdmin";
            ViewBag.Today = DateTime.Today;
            return View();
        }
    }
}
