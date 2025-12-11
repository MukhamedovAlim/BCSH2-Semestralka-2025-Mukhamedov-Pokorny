using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models.Admin;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminMemberships")]
    public sealed class AdminMembershipsController : Controller
    {
        // výchozí ceny, když je DB nedodá
        private static readonly Dictionary<string, decimal> DEFAULT_PRICES =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Jednorázové"] = 250m,
                ["Měsíční"] = 990m,
                ["Roční"] = 7990m
            };

        // načti typy členství (název + cena), s fallbackem na DEFAULT_PRICES
        private static async Task<List<(string Name, decimal Price)>> LoadTypyAsync()
        {
            var list = new List<(string, decimal)>();
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            try
            {
                using var cmd = new OracleCommand("SELECT NAZEV, CENA FROM TYPYCLENSTVI ORDER BY NAZEV", con);
                using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    var name = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var priceDb = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1);
                    var price = priceDb > 0 ? priceDb :
                                  (DEFAULT_PRICES.TryGetValue(name, out var p) ? p : 0m);

                    list.Add((name, price));
                }
            }
            catch (OracleException ox) when (ox.Number == 904)
            {
                // sloupec CENA neexistuje → vezmeme jen názvy
                using var cmd = new OracleCommand("SELECT NAZEV FROM TYPYCLENSTVI ORDER BY NAZEV", con);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var name = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var price = DEFAULT_PRICES.TryGetValue(name, out var p) ? p : 0m;
                    list.Add((name, price));
                }
            }

            // kdyby bylo úplně prázdno → aspoň defaulty
            if (list.Count == 0)
                foreach (var kv in DEFAULT_PRICES) list.Add((kv.Key, kv.Value));

            return list;
        }

        // členové pro combobox
        private static async Task<List<SelectListItem>> LoadMembersForSelectAsync()
        {
            var items = new List<SelectListItem>();
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
                SELECT email, jmeno || ' ' || prijmeni AS nm
                FROM CLENOVE
                WHERE email IS NOT NULL
                ORDER BY prijmeni, jmeno", con);
            using var rd = await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                var email = rd.IsDBNull(0) ? "" : rd.GetString(0);
                var text = rd.IsDBNull(1) ? email : $"{rd.GetString(1)}  <{email}>";
                if (!string.IsNullOrWhiteSpace(email))
                    items.Add(new SelectListItem { Value = email, Text = text });
            }
            return items;
        }

        private static decimal PriceByType(string? t) =>
            DEFAULT_PRICES.TryGetValue(t ?? "", out var p) ? p : 0m;

        [HttpGet("Sell")]
        public async Task<IActionResult> Sell()
        {
            var typy = await LoadTypyAsync();
            if (typy.Count == 0)
                typy = DEFAULT_PRICES.Select(kv => (kv.Key, kv.Value)).ToList();

            var members = await LoadMembersForSelectAsync();

            ViewBag.Typy = typy.Select(t => new SelectListItem { Value = t.Name, Text = t.Name }).ToList();
            ViewBag.Members = members;

            // předvyplň první typ a jeho cenu
            var first = typy.First();
            var vm = new SellMembershipViewModel
            {
                TypNazev = first.Name,
                Castka = first.Price
            };

            if (members.Count == 0)
                TempData["Err"] = "V systému zatím nejsou žádní členové s e-mailem. Nejprve někoho založ.";

            return View(vm);
        }


        // ───── POST: /AdminMemberships/Sell ─────
        [HttpPost("Sell")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sell(SellMembershipViewModel vm)
        {
            async Task FillBagsAsync()
            {
                var typy = await LoadTypyAsync();
                ViewBag.Typy = typy.Select(t => new SelectListItem { Value = t.Name, Text = t.Name }).ToList();
                ViewBag.Members = await LoadMembersForSelectAsync();
            }

            vm.Email = vm.Email?.Trim() ?? string.Empty;
            vm.TypNazev = vm.TypNazev?.Trim() ?? string.Empty;

            // doplň částku podle typu, pokud není > 0
            if (vm.Castka <= 0 && !string.IsNullOrWhiteSpace(vm.TypNazev))
                vm.Castka = PriceByType(vm.TypNazev);

            // validace
            if (string.IsNullOrWhiteSpace(vm.Email))
                ModelState.AddModelError(nameof(vm.Email), "Vyber člena (e-mail).");
            if (string.IsNullOrWhiteSpace(vm.TypNazev))
                ModelState.AddModelError(nameof(vm.TypNazev), "Vyber typ členství.");
            if (vm.Castka <= 0)
                ModelState.AddModelError(nameof(vm.Castka), "Částka musí být větší než 0.");

            if (!ModelState.IsValid)
            {
                var errs = string.Join("; ",
                    ModelState.Where(kv => kv.Value!.Errors.Count > 0)
                              .SelectMany(kv => kv.Value!.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}")));
                TempData["Err"] = $"Neplatný formulář: {errs}";
                await FillBagsAsync();
                return View(vm);
            }

            // volání procedury
            try
            {
                using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("PRODEJ_CLENSTVI_EXISTUJICIMU", con)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };

                cmd.Parameters.Add("p_email", OracleDbType.Varchar2, vm.Email, ParameterDirection.Input);
                cmd.Parameters.Add("p_typ_nazev", OracleDbType.Varchar2, vm.TypNazev, ParameterDirection.Input);
                cmd.Parameters.Add("p_castka", OracleDbType.Decimal, vm.Castka, ParameterDirection.Input);
                if (vm.IhnedZaplaceno)
                    cmd.Parameters.Add("p_stav_nazev", OracleDbType.Varchar2, "Zaplaceno", ParameterDirection.Input);

                var pOutCl = new OracleParameter("p_idclenstvi_out", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                var pOutPl = new OracleParameter("p_idplatba_out", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(pOutCl);
                cmd.Parameters.Add(pOutPl);

                await cmd.ExecuteNonQueryAsync();

                TempData["Ok"] = $"Členství „{vm.TypNazev}“ prodáno (částka {vm.Castka:0} Kč).";
                return RedirectToAction("Admin", "Home");
            }
            catch (OracleException ox)
            {
                TempData["Err"] = "DB chyba při prodeji členství: " + ox.Message;
                await FillBagsAsync();
                return View(vm);
            }
            catch (System.Exception ex)
            {
                TempData["Err"] = "Prodej členství selhal: " + ex.Message;
                await FillBagsAsync();
                return View(vm);
            }
        }
    }
}
