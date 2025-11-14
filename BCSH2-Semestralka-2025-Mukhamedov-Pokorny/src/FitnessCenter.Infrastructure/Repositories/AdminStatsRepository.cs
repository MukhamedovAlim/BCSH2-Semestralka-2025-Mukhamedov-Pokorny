using System;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class AdminStatsRepository
    {
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        // f_pocet_aktivnich_clenu(p_idfitness) -> INT
        public async Task<int> GetActiveMembersAsync(int fitkoId)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("SELECT f_pocet_aktivnich_clenu(:p) FROM dual", con)
            { BindByName = true, CommandType = CommandType.Text };
            cmd.Parameters.Add("p", OracleDbType.Int32).Value = fitkoId;

            var val = await cmd.ExecuteScalarAsync();
            if (val is string s)
            {
                foreach (var tok in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(tok, out var n)) return n;
                return 0;
            }
            return Convert.ToInt32(val);
        }

        // f_prijem_obdobi(p_od, p_do) -> NUMBER
        public async Task<decimal> GetIncomeAsync(DateTime from, DateTime to)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("SELECT f_prijem_obdobi(:od,:do) FROM dual", con)
            { BindByName = true, CommandType = CommandType.Text };
            cmd.Parameters.Add("od", OracleDbType.Date).Value = from;
            cmd.Parameters.Add("do", OracleDbType.Date).Value = to;

            var val = await cmd.ExecuteScalarAsync();
            return Convert.ToDecimal(val ?? 0m);
        }

        // pomocná: najdi nějakou relevantní lekci (třeba nejbližší budoucí)
        public async Task<int?> GetLatestUpcomingLessonIdAsync()
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(
                @"SELECT idlekce 
                    FROM lekce 
                   WHERE datumlekce >= SYSDATE 
                ORDER BY datumlekce 
                   FETCH FIRST 1 ROWS ONLY", con);
            var v = await cmd.ExecuteScalarAsync();
            return v == null ? (int?)null : Convert.ToInt32(v);
        }

        // f_statistika_lekce(p_idlekce) -> VARCHAR2 (text)
        public async Task<string> GetLessonStatAsync(int idLekce)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("SELECT f_statistika_lekce(:id) FROM dual", con)
            { BindByName = true, CommandType = CommandType.Text };
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = idLekce;

            var val = await cmd.ExecuteScalarAsync();
            return Convert.ToString(val) ?? "—";
        }
    }
}
