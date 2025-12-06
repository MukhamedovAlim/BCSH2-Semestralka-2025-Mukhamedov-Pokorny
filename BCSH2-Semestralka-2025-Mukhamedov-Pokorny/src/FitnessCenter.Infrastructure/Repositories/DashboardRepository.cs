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
                mesice.Add(reader.GetString(0));    // MESIC (např. 2025-11)
                trzby.Add(reader.GetDecimal(1));    // TRZBA
            }

            return (mesice, trzby);
        }

        // --- DENNÍ TRŽBY pro HomeController.Admin ---
        public async Task<(List<string> Dny, List<decimal> Trzby)> GetDailyRevenueAsync(
            DateTime from, DateTime to)
        {
            var dny = new List<string>();
            var trzby = new List<decimal>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    TRUNC(DATUMPLATBY) AS DEN,
                    SUM(CASTKA)        AS TRZBA
                FROM PLATBY
                WHERE STAVPLATBY_IDSTAVPLATBY = 2
                  AND DATUMPLATBY >= :od
                  AND DATUMPLATBY <  :do_plus1
                GROUP BY TRUNC(DATUMPLATBY)
                ORDER BY DEN";

            cmd.BindByName = true;
            cmd.Parameters.Add("od", OracleDbType.Date).Value = from.Date;
            cmd.Parameters.Add("do_plus1", OracleDbType.Date).Value = to.Date.AddDays(1);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var den = reader.GetDateTime(0);
                dny.Add(den.ToString("dd.MM.yyyy"));
                trzby.Add(reader.GetDecimal(1));
            }

            return (dny, trzby);
        }

        public async Task<(List<string> Typy, List<int> Pocty)> GetMembershipDistributionAsync()
        {
            var typy = new List<string>();
            var pocty = new List<int>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
        SELECT 
            TYPCLENSTVI_IDTYPCLENSTVI AS TYP_ID,
            COUNT(*)                  AS POCET
        FROM CLENSTVI
        GROUP BY TYPCLENSTVI_IDTYPCLENSTVI
        ORDER BY TYP_ID";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int typId = reader.GetInt32(0);
                int pocet = reader.GetInt32(1);

                string nazev = typId switch
                {
                    1 => "Jednorázové",
                    2 => "Měsíční",
                    3 => "Roční",
                    _ => $"Typ {typId}"
                };

                typy.Add(nazev);
                pocty.Add(pocet);
            }

            return (typy, pocty);
        }

        public async Task<(List<string> Treneri, List<int> Pocty)> GetTopTrainersAsync()
        {
            var treneri = new List<string>();
            var pocty = new List<int>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
        SELECT *
        FROM (
            SELECT
                t.JMENO || ' ' || t.PRIJMENI AS TRENER,
                COUNT(*)                     AS POCET
            FROM TRENERI t
            JOIN LEKCE l
                ON l.TRENER_IDTRENER = t.IDTRENER
            JOIN RELEKCI rlk
                ON rlk.LEKCE_IDLEKCE = l.IDLEKCE
            JOIN REZERVACELEKCI rz
                ON rz.CLEN_IDCLEN   = rlk.REZERVACELEKCI_CLEN_IDCLEN
               AND rz.IDREZERVACE   = rlk.REZERVACELEKCI_IDREZERVACE
            GROUP BY t.JMENO, t.PRIJMENI
            ORDER BY POCET DESC
        )
        WHERE ROWNUM <= 5";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string jmeno = reader.GetString(0); // TRENER
                int pocet = reader.GetInt32(1);     // POCET

                treneri.Add(jmeno);
                pocty.Add(pocet);
            }

            return (treneri, pocty);
        }
    }
}