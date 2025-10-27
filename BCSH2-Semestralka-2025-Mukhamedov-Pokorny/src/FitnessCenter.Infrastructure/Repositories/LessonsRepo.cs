using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using FitnessCenter.Infrastructure.Persistence;   // DatabaseManager

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class LessonsRepo
    {
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        // --- lookup člena podle e-mailu (hodí se pro fallback) ---
        public async Task<int?> GetMemberIdByEmailAsync(string email)
        {
            const string sql = "SELECT idclen FROM clenove WHERE LOWER(email)=LOWER(:email)";
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("email", email);
            var o = await cmd.ExecuteScalarAsync();
            return (o == null || o is DBNull) ? (int?)null : Convert.ToInt32(o);
        }

        // --- view v_lekce_volne ---
        public sealed record LessonRow(int Id, string Nazev, DateTime Datum, int Kapacita, int Obsazeno, int Volno);
        public async Task<List<LessonRow>> GetUpcomingLessonsAsync()
        {
            const string sql = @"SELECT idlekce, nazevlekce, datumlekce, kapacita, obsazeno, volno
                                 FROM v_lekce_volne ORDER BY datumlekce";
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con);
            using var rd = await cmd.ExecuteReaderAsync();

            var list = new List<LessonRow>();
            while (await rd.ReadAsync())
                list.Add(new LessonRow(
                    rd.GetInt32(0), rd.GetString(1), rd.GetDateTime(2),
                    rd.GetInt32(3), rd.GetInt32(4), rd.GetInt32(5)));
            return list;
        }

        // --- view v_moje_rezervace ---
        public sealed record MyReservation(int IdLekce, int IdClen, int IdRez, DateTime KdyRez, string Nazev, DateTime KdyLekce);
        public async Task<List<MyReservation>> GetMyReservationsAsync(int idClen)
        {
            const string sql = @"SELECT idlekce, id_clena, idrezervace, datumrezervace, nazevlekce, datumlekce
                                 FROM v_moje_rezervace
                                 WHERE id_clena=:id
                                 ORDER BY datumlekce";
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", idClen);
            using var rd = await cmd.ExecuteReaderAsync();

            var list = new List<MyReservation>();
            while (await rd.ReadAsync())
                list.Add(new MyReservation(
                    rd.GetInt32(0), rd.GetInt32(1), rd.GetInt32(2),
                    rd.GetDateTime(3), rd.GetString(4), rd.GetDateTime(5)));
            return list;
        }

        // --- PROC: rezervovat_lekci(p_idclen, p_idlecke, p_idrez_out) ---
        public async Task<int> ReserveAsync(int idClen, int idLekce)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("rezervovat_lekci", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };
            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = idClen;
            cmd.Parameters.Add("p_idlecke", OracleDbType.Int32).Value = idLekce;
            cmd.Parameters.Add("p_idrez_out", OracleDbType.Int32).Direction = ParameterDirection.Output;

            try
            {
                await cmd.ExecuteNonQueryAsync();
                return Convert.ToInt32(cmd.Parameters["p_idrez_out"].Value.ToString());
            }
            catch (OracleException ox) when (ox.Number == 20022)
            {
                throw new InvalidOperationException("Lekce je plně obsazena.");
            }
            catch (OracleException ox) when (ox.Number == 20023)
            {
                throw new InvalidOperationException("Už máš rezervaci na tuto lekci.");
            }
        }

        // zrusit_rezervaci(p_idlekce, p_idclen) ---
        public async Task CancelAsync(int idLekce, int idClen)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("zrusit_rezervaci", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };
            cmd.Parameters.Add("p_idlekce", OracleDbType.Int32).Value = idLekce;
            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = idClen;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
