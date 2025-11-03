using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member; // DatabaseManager
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // ===== LIST (z DB view V_CLENOVE_PUBLIC) =====
        // SELECT idclen, jmeno, prijmeni, email_mask FROM V_CLENOVE_PUBLIC
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
                    ORDER BY prijmeni, jmeno", conn);

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
                        // volitelné flagy jen pro UI (nebo si je načti jinak)
                        IsActive = true
                    });
                }
            }
            catch (Exception ex)
            {
                // přátelské hlášení do UI + prázdný list, ať stránka nespadne
                TempData["Err"] = "Nepodařilo se načíst seznam členů: " + ex.Message;
                list = new List<MemberViewModel>();
            }

            return View(list);
        }

        // ===== Zbytek nechávám podle tvého stávajícího flow =====

        // GET: /Members/Details/5
        public IActionResult Details(int id)
        {
            // Pokud budeš chtít detail také z DB, doplníme dotaz na CLENOVE podle id
            return NotFound(); // nebo si nech tvůj původní demo kód
        }
        private readonly IMembersService _members;
        public MembersController(IMembersService members) => _members = members;

        [HttpGet]
        public IActionResult Create()
            => View(new MemberCreateViewModel { FitnessCenterId = 1 });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (!vm.BirthDate.HasValue)
            {
                ModelState.AddModelError(nameof(vm.BirthDate), "Zadej datum narození.");
                return View(vm);
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
                // ⬅️ TADY PROBĚHNE VLOŽENÍ DO DB PŘES PROCEDURU
                var newId = await _members.CreateViaProcedureAsync(m);

                TempData["Ok"] = $"Člen byl vytvořen (ID: {newId}).";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // např. ORA-00001 (unikátní email/telefon)
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        // GET: /Members/Edit/5
        public IActionResult Edit(int id)
        {
            // TODO: načíst CLENOVE podle id a předvyplnit
            return NotFound();
        }

        // POST: /Members/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Edit(int id, MemberViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // TODO: update CLENOVE (procedura/SQL)
            TempData["Ok"] = "Změny uloženy (doplníme update v DB).";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Members/Delete/5
        public IActionResult Delete(int id)
        {
            // TODO: načíst CLENOVE a potvrzovací view
            return NotFound();
        }

        // POST: /Members/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            // TODO: smazat z CLENOVE (procedura/SQL)
            TempData["Ok"] = "Člen byl odstraněn (doplníme delete v DB).";
            return RedirectToAction(nameof(Index));
        }
    }
}
