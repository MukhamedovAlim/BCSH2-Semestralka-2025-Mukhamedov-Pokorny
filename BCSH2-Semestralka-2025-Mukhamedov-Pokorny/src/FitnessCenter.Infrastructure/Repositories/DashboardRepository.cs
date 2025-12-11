using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class DashboardRepository
    {
        // --- MĚSÍČNÍ TRŽBY pro AdminDashboardController ---
        public async Task<(List<string> Mesice, List<decimal> Trzby)> GetMonthlyRevenueAsync()
        {
            var mesice = new List<string>();
            var trzby = new List<decimal>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = con.CreateCommand();

            cmd.CommandText = @"
                SELECT MESIC, TRZBA
                FROM V_TRZBY_MESIC
                ORDER BY MESIC";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                mesice.Add(reader.GetString(0));   // MESIC (např. 2025-11)
                trzby.Add(reader.GetDecimal(1));   // TRZBA
            }

            return (mesice, trzby);
        }

        // --- DENNÍ TRŽBY pro HomeController.Admin ---
        public async Task<(List<string> Days, List<decimal> Revenue)> GetDailyRevenueAsync(
        DateTime from,
        DateTime to)
        {
            var byDay = new Dictionary<DateTime, decimal>();

            using var con = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
            SELECT TRUNC(p.datumplatby) AS den,
                   SUM(p.castka)        AS suma
            FROM   platby p
            WHERE  p.datumplatby >= :p_from
               AND p.datumplatby <= :p_to
               AND p.stavplatby_idstavplatby = 2      -- <<< jen zaplacené platby
            GROUP BY TRUNC(p.datumplatby)
            ORDER BY den
        ", (OracleConnection)con)
            { BindByName = true };

            cmd.Parameters.Add("p_from", OracleDbType.Date).Value = from.Date;
            cmd.Parameters.Add("p_to", OracleDbType.Date).Value = to.Date;

            using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    var den = rd.GetDateTime(0).Date;
                    var suma = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1);
                    byDay[den] = suma;
                }
            }

            var labels = new List<string>();
            var values = new List<decimal>();

            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                labels.Add(d.ToString("dd.MM.yyyy"));
                values.Add(byDay.TryGetValue(d, out var suma) ? suma : 0m);
            }

            return (labels, values);
        }

        // --- PODÍL TYPŮ ČLENSTVÍ pro koláčový graf ---
        public async Task<(List<string> Typy, List<int> Pocty)> GetMembershipDistributionAsync()
        {
            var typy = new List<string>();
            var pocty = new List<int>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            using var cmd = new OracleCommand(@"
        SELECT 
            c.TYPCLENSTVI_IDTYPCLENSTVI AS TYP_ID,
            COUNT(*)                   AS POCET
        FROM CLENSTVI c
        -- pokud chceš jen aktuální členství, můžeš sem doplnit podmínku:
        -- WHERE c.DAT_DO IS NULL OR c.DAT_DO >= TRUNC(SYSDATE)
        GROUP BY c.TYPCLENSTVI_IDTYPCLENSTVI
        ORDER BY TYP_ID
    ", con)
            { BindByName = true };

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int typId = reader.GetInt32(0);
                int pocet = reader.GetInt32(1);

                string nazev = typId switch
                {
                    1 => "Měsíční",
                    2 => "Roční",
                    3 => "Jednorázové",
                    _ => $"Typ {typId}"
                };

                typy.Add(nazev);
                pocty.Add(pocet);
            }

            return (typy, pocty);
        }

        // --- TOP TRENÉŘI podle počtu rezervací ---
        public async Task<(List<string> TrainerNames, List<int> ReservationCounts)> GetTopTrainersAsync()
        {
            var names = new List<string>();
            var counts = new List<int>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            const string sql = @"
        SELECT *
        FROM (
            SELECT t.jmeno || ' ' || t.prijmeni AS trener,
                   COUNT(*)                    AS pocet
            FROM   treneri t
                   JOIN lekce l
                     ON l.trener_idtrener = t.idtrener
                   JOIN relekci rl
                     ON rl.lekce_idlekce = l.idlekce
            GROUP  BY t.jmeno, t.prijmeni
            ORDER  BY pocet DESC
        )
        WHERE ROWNUM <= 5";

            using var cmd = new OracleCommand(sql, con) { BindByName = true };

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var name = rd.IsDBNull(0) ? "Neznámý trenér" : rd.GetString(0);
                var count = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);

                names.Add(name);
                counts.Add(count);
            }

            return (names, counts);
        }
    }
}
