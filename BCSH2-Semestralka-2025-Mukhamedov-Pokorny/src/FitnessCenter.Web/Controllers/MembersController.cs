using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using FitnessCenter.Infrastructure.Persistence; // DatabaseManager

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

        // GET: /Members/Create
        public IActionResult Create() => View(new MemberViewModel());

        // POST: /Members/Create
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Create(MemberViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // TODO: uložení do CLENOVE (procedura/SQL). Zatím jen info.
            TempData["Ok"] = "Člen byl vytvořen (doplníme uložení do DB).";
            return RedirectToAction(nameof(Index));
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
