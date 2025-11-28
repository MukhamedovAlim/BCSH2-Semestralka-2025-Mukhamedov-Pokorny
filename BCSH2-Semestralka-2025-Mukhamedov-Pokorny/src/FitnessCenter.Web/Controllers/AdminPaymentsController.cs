using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminPaymentsController : Controller
    {
        private readonly PaymentsReadRepo _read;
        private readonly PaymentsWriteRepo _write;

        public AdminPaymentsController(PaymentsReadRepo read, PaymentsWriteRepo write)
        {
            _read = read;
            _write = write;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "AdminPayments";

            var rows = await _read.GetPendingPaymentsAsync();

            var vm = rows.Select(r => new AdminPaymentViewModel
            {
                IdPlatba = r.IdPlatba,
                MemberId = r.MemberId,
                MemberName = r.MemberName,
                Email = r.Email,
                Datum = r.Datum,
                Castka = r.Castka,
                Stav = r.Stav
            }).ToList();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                await _write.ApproveMembershipPaymentAsync(id);
                TempData["Ok"] = $"Platba byla schválena.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = $"Schválení platby {id} selhalo: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            try
            {
                await _write.RejectMembershipPaymentAsync(id);
                TempData["Ok"] = $"Platba byla zamítnuta.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = $"Zamítnutí platby {id} selhalo: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
