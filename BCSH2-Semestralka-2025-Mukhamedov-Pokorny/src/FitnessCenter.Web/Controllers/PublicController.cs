using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using FitnessCenter.Infrastructure.Persistence; // DatabaseManager

namespace FitnessCenter.Web.Controllers
{
    [AllowAnonymous]
    public class PublicController : Controller
    {
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
                    list.Add((rd.GetInt32(0), rd.GetString(1), rd.GetDateTime(2), rd.GetInt32(3)));
                }
            }
            catch { /* veřejná stránka – chybu nevyhazuj, jen zobraz prázdné */ }

            return View(list);
        }

        // Příklad: veřejný seznam trenérů (jen jméno; bez kontaktů)
        public async Task<IActionResult> Trainers()
        {
            var list = new List<string>();
            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
                    SELECT (jmeno || ' ' || prijmeni) 
                    FROM treneri
                    ORDER BY prijmeni, jmeno", (OracleConnection)con);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) list.Add(rd.GetString(0));
            }
            catch { }
            return View(list);
        }
    }
}
