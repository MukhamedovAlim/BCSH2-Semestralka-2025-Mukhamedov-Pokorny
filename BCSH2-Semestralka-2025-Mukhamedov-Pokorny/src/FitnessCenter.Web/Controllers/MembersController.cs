using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member; // MemberViewModel
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MembersController : Controller
    {
        private readonly IMembersService _members;

        public MembersController(IMembersService members)
        {
            _members = members;
        }

        // ---------------- helper: číselník fitness center ----------------
        private static async Task<List<SelectListItem>> LoadFitnessForSelectAsync()
        {
            var items = new List<SelectListItem>();
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                "SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev",
                (OracleConnection)conn);

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

        // ===== LIST (z DB view V_CLENOVE_PUBLIC) =====
        // SELECT idclen, jmeno, prijmeni, email_mask FROM V_CLENOVE_PUBLIC
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Active = "Members";
            ViewBag.HideMainNav = true; // schová hlavní položky v _AppLayout

            var list = new List<MemberViewModel>();

            try
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
                    SELECT idclen, jmeno, prijmeni, email_mask
                    FROM V_CLENOVE_PUBLIC
                    ORDER BY prijmeni, jmeno", (OracleConnection)conn);

                using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    list.Add(new MemberViewModel
                    {
                        MemberId = rd.GetInt32(0),
                        FirstName = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        LastName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Email = rd.IsDBNull(3) ? null : rd.GetString(3), // maskovaný e-mail
                        Phone = null,                                     // ve view je NULL
                        IsActive = true
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst seznam členů: " + ex.Message;
                list = new List<MemberViewModel>();
            }

            return View(list);
        }

        // GET: /Members/Details/5
        [HttpGet]
        public IActionResult Details(int id)
        {
            return NotFound();
        }

        // ======================= CREATE =======================
        // GET
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(); // naplnění dropdownu
            return View(new MemberCreateViewModel { FitnessCenterId = 0 });
        }

        // POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(vm);
            }

            if (!vm.BirthDate.HasValue)
            {
                ModelState.AddModelError(nameof(vm.BirthDate), "Zadej datum narození.");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(vm);
            }

            if (vm.FitnessCenterId <= 0)
            {
                ModelState.AddModelError(nameof(vm.FitnessCenterId), "Vyber fitness centrum.");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(vm);
            }

            // Volitelná pojistka: zvolený IDFITNESS existuje?
            using (var conn = await DatabaseManager.GetOpenConnectionAsync())
            using (var chk = new OracleCommand("SELECT COUNT(*) FROM fitnesscentra WHERE idfitness=:id", (OracleConnection)conn))
            {
                chk.BindByName = true;
                chk.Parameters.Add("id", vm.FitnessCenterId);
                var exists = Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    ModelState.AddModelError(nameof(vm.FitnessCenterId), "Zvolené fitness centrum neexistuje.");
                    ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                    return View(vm);
                }
            }

            var m = new Member
            {
                FirstName = vm.FirstName?.Trim() ?? "",
                LastName = vm.LastName?.Trim() ?? "",
                Email = vm.Email?.Trim() ?? "",
                Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone!.Trim(),
                Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address!.Trim(),
                BirthDate = vm.BirthDate.Value,
                FitnessCenterId = vm.FitnessCenterId
            };

            try
            {
                var newId = await _members.CreateViaProcedureAsync(m); // PR_CLEN_CREATE
                TempData["Ok"] = $"Člen byl vytvořen (ID: {newId}).";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // např. ORA-00001 (unikátní email/telefon)
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(vm);
            }
        }

        // ======================= EDIT =======================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            return NotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, MemberViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            TempData["Ok"] = "Změny uloženy (doplníme update v DB).";
            return RedirectToAction(nameof(Index));
        }

        // ======================= DELETE =======================
        [HttpGet]
        public IActionResult Delete(int id)
        {
            return NotFound();
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Ok"] = "Člen byl odstraněn (doplníme delete v DB).";
            return RedirectToAction(nameof(Index));
        }
    }
}
