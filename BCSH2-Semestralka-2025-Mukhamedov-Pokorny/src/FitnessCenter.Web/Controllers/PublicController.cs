using FitnessCenter.Infrastructure.Persistence; // DatabaseManager
using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [AllowAnonymous]
    public class PublicController : Controller
    {
        private readonly EquipmentRepository _equipmentRepo;

        public PublicController(EquipmentRepository equipmentRepo)
        {
            _equipmentRepo = equipmentRepo;
        }

        public IActionResult Index()
        {
            ViewBag.Active = "Public";
            return View();
        }

        // Veřejný rozvrh – bez osobních údajů, jen název, datum, volná místa
        public async Task<IActionResult> Lessons()
        {
            var list = new List<(int Id, string Nazev, DateTime Datum, int Volno)>();

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
                    SELECT idlekce, nazevlekce, datumlekce, volno
                    FROM v_lekce_volne
                    WHERE datumlekce >= SYSDATE
                    ORDER BY datumlekce", (OracleConnection)con);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    list.Add((
                        rd.GetInt32(0),
                        rd.GetString(1),
                        rd.GetDateTime(2),
                        rd.GetInt32(3)
                    ));
                }
            }
            catch
            {
                // Chyba při načítání – zobrazit prázdný seznam
            }

            return View(list);
        }

        // Veřejný seznam trenérů – stejné view modely jako pro člena
        public async Task<IActionResult> Trainers()
        {
            var list = new List<MemberTrainerListItem>();

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
                    SELECT idtrener,
                           jmeno,
                           prijmeni,
                           telefon
                    FROM treneri
                    ORDER BY prijmeni, jmeno", (OracleConnection)con);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var id = rd.GetInt32(0);
                    var jmeno = rd.GetString(1);
                    var prijmeni = rd.GetString(2);
                    var telefon = rd.IsDBNull(3) ? "" : rd.GetString(3);

                    list.Add(new MemberTrainerListItem
                    {
                        Id = id,
                        Jmeno = jmeno,
                        Prijmeni = prijmeni,
                        Telefon = telefon
                    });
                }
            }
            catch
            {
            }

            return View(list);
        }

        // Veřejný detail trenéra – stejný layout jako MemberTrainerDetail
        public async Task<IActionResult> TrainerDetail(int id)
        {
            MemberTrainerDetailViewModel? model = null;

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();

                // 1) Základní info o trenérovi
                using (var cmd = new OracleCommand(@"
                    SELECT idtrener,
                           jmeno,
                           prijmeni,
                           email,
                           telefon
                    FROM treneri
                    WHERE idtrener = :id", (OracleConnection)con))
                {
                    cmd.Parameters.Add(new OracleParameter("id", id));

                    using var rd = await cmd.ExecuteReaderAsync();
                    if (!await rd.ReadAsync())
                    {
                        return NotFound();
                    }

                    model = new MemberTrainerDetailViewModel
                    {
                        TrenerId = rd.GetInt32(0),
                        Jmeno = rd.GetString(1),
                        Prijmeni = rd.GetString(2),
                        Email = rd.IsDBNull(3) ? "" : rd.GetString(3),
                        Telefon = rd.IsDBNull(4) ? "" : rd.GetString(4)
                    };
                }

                // 2) Nadcházející lekce daného trenéra
                var lekceList = new List<MemberTrainerLessonRow>();

                using (var cmd = new OracleCommand(@"
                    SELECT l.idlekce,
                           l.nazevlekce,
                           l.datumlekce,
                           l.obsazenost
                    FROM lekce l
                    WHERE l.trener_idtrener = :id
                      AND l.datumlekce >= SYSDATE
                    ORDER BY l.datumlekce", (OracleConnection)con))
                {
                    cmd.Parameters.Add(new OracleParameter("id", id));

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        lekceList.Add(new MemberTrainerLessonRow
                        {
                            IdLekce = rd.GetInt32(0),
                            Nazev = rd.GetString(1),
                            Datum = rd.GetDateTime(2),
                            Obsazenost = rd.GetInt32(3)
                        });
                    }
                }

                model.Lekce = lekceList;
                model.PocetLekci = lekceList.Count;
            }
            catch
            {
            }

            if (model == null)
                return NotFound();

            return View(model);
        }

        // === PUBLIC VYBAVENÍ ==========================================
        public async Task<IActionResult> Equipments(string? typ, int? fitko)
        {
            ViewBag.Active = "Public";

            // Stejná logika filtru jako v EquipmentController.Index
            string filterLabel = typ switch
            {
                "K" => "Kardio",
                "P" => "Posilovací",
                "V" => "Volná závaží",
                _ => "Vše"
            };

            if (filterLabel == "Vše")
                typ = null;

            ViewBag.Filter = filterLabel;
            ViewBag.SelectedTyp = typ;
            ViewBag.FitkoId = fitko;

            // Fitka – použijeme stejný helper jako v EquipmentController,
            // jen zkopírovaný sem s jiným jménem, ať je to nezávislé.
            var fitka = await LoadFitnessCentersForPublicAsync();
            ViewBag.Fitka = fitka;

            // Data přes _repo – úplně stejně jako interní Index
            var rows = await _equipmentRepo.GetAsync(typ, fitko);
            var vm = rows.Select(r => new EquipmentViewModel
            {
                Id = r.Id,
                Nazev = r.Nazev,
                Typ = r.Typ switch
                {
                    "K" => "Kardio",
                    "P" => "Posilovací",
                    "V" => "Volná závaží",
                    _ => r.Typ
                },
                Stav = r.Stav,
                Fitko = r.Fitko,
                FitkoId = r.FitkoId
            }).ToList();

            return View(vm); // Views/Public/Equipments.cshtml
        }

        // stejný dotaz na fitness centra jako v EquipmentController,
        // jen přejmenovaný
        private static async Task<List<SelectListItem>> LoadFitnessCentersForPublicAsync()
        {
            var items = new List<SelectListItem>();
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                "SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev", con);
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
    }
}
