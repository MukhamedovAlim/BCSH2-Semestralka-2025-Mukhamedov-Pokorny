using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;             // Lesson
using FitnessCenter.Infrastructure.Persistence;  // DatabaseManager
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace FitnessCenter.Infrastructure.Repositories
{
    /// <summary>
    /// Oracle repo pro lekce a rezervace – volá uložené procedury (REF CURSOR) a jednoduché CRUDy.
    /// </summary>
    public sealed class OracleLessonsRepository : ILessonRepository
    {
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        // ---------------------------------------------------------------------
        // 1) LEKCE – nadcházející přes proceduru SP_LESSONS_UPCOMING
        // ---------------------------------------------------------------------
        public async Task<List<Lesson>> GetUpcomingViaProcAsync(DateTime? from = null, int? trainerId = null)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("SP_LESSONS_UPCOMING", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_from", OracleDbType.Date).Value = (object?)from ?? DBNull.Value;
            cmd.Parameters.Add("p_trener_id", OracleDbType.Int32).Value = (object?)trainerId ?? DBNull.Value;
            cmd.Parameters.Add("p_rc", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            using var rc = (OracleRefCursor)cmd.Parameters["p_rc"].Value;
            using var rd = rc.GetDataReader();

            var list = new List<Lesson>();
            while (rd.Read())
            {
                list.Add(new Lesson
                {
                    Id = rd.GetInt32(0),        // idlekce
                    Nazev = rd.GetString(1),    // nazevlekce
                    Zacatek = rd.GetDateTime(2),// datumlekce
                    Kapacita = rd.GetInt32(3),  // kapacita (obsazenost)
                    Mistnost = string.Empty,
                    Popis = null
                });
            }
            return list;
        }

        // ---------------------------------------------------------------------
        // 2) MOJE REZERVACE – SP_MY_RESERVATIONS (z view v_clen_rezervace)
        //    POŘADÍ SLOUPCŮ: idlekce, id_clena, idrezervace, datumrezervace, nazevlekce, datumlekce
        // ---------------------------------------------------------------------
        public sealed record MyReservationRow(
            int IdLekce, int IdClen, int IdRezervace,
            DateTime DatumRezervace, string Nazev, DateTime DatumLekce);

        public async Task<List<MyReservationRow>> GetMyReservationsViaProcAsync(int idClen)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("SP_MY_RESERVATIONS", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = idClen;
            cmd.Parameters.Add("p_rc", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            using var rc = (OracleRefCursor)cmd.Parameters["p_rc"].Value;
            using var rd = rc.GetDataReader();

            var list = new List<MyReservationRow>();
            while (rd.Read())
            {
                list.Add(new MyReservationRow(
                    rd.GetInt32(0),      // idlekce
                    rd.GetInt32(1),      // id_clena
                    rd.GetInt32(2),      // idrezervace
                    rd.GetDateTime(3),   // datumrezervace
                    rd.GetString(4),     // nazevlekce
                    rd.GetDateTime(5)    // datumlekce
                ));
            }
            return list;
        }

        // ---------------------------------------------------------------------
        // 3) REZERVACE – vytvořit / zrušit
        //    - REZERVACE:   rezervovat_lekci(p_idclen, p_idlecke, p_idrez_out OUT)
        //    - ZRUŠENÍ:     SP_CANCEL_RESERVATION(p_idclen, p_idrez)
        // ---------------------------------------------------------------------
        public async Task<int> ReserveLessonAsync(int idClen, int idLekce)
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

            await cmd.ExecuteNonQueryAsync();

            return Convert.ToInt32(cmd.Parameters["p_idrez_out"].Value.ToString());
        }

        /// <summary>
        /// Zruší rezervaci podle ID rezervace; vlastnictví kontroluje samotná procedura v DB.
        /// </summary>
        public async Task CancelReservationByIdAsync(int idClen, int idRezervace)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("SP_CANCEL_RESERVATION", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = idClen;
            cmd.Parameters.Add("p_idrez", OracleDbType.Int32).Value = idRezervace;

            await cmd.ExecuteNonQueryAsync();
        }

        // ---------------------------------------------------------------------
        // 4) ILessonRepository – jednoduché CRUDy pro tabulku LEKCE
        // ---------------------------------------------------------------------
        public async Task<IEnumerable<Lesson>> GetAllAsync(CancellationToken ct = default)
        {
            const string sql = @"
                SELECT idlekce, nazevlekce, datumlekce, obsazenost
                  FROM lekce
                 ORDER BY datumlekce";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con);
            using var rd = await cmd.ExecuteReaderAsync(ct);

            var list = new List<Lesson>();
            while (await rd.ReadAsync(ct))
            {
                list.Add(new Lesson
                {
                    Id = rd.GetInt32(0),
                    Nazev = rd.GetString(1),
                    Zacatek = rd.GetDateTime(2),
                    Kapacita = rd.GetInt32(3),
                    Mistnost = string.Empty,
                    Popis = null
                });
            }
            return list;
        }

        public async Task<Lesson?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            const string sql = @"
                SELECT idlekce, nazevlekce, datumlekce, obsazenost
                  FROM lekce
                 WHERE idlekce = :id";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

            using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                return new Lesson
                {
                    Id = rd.GetInt32(0),
                    Nazev = rd.GetString(1),
                    Zacatek = rd.GetDateTime(2),
                    Kapacita = rd.GetInt32(3),
                    Mistnost = string.Empty,
                    Popis = null
                };
            }
            return null;
        }

        public async Task<int> CreateAsync(Lesson lesson, CancellationToken ct = default)
        {
            const string sql = @"
                INSERT INTO lekce (idlekce, nazevlekce, datumlekce, obsazenost, trener_idtrener)
                VALUES (S_LEKCE.NEXTVAL, :nazev, :datum, :kapacita, :idtrener)
                RETURNING idlekce INTO :idout";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };

            cmd.Parameters.Add("nazev", OracleDbType.Varchar2).Value = lesson.Nazev;
            cmd.Parameters.Add("datum", OracleDbType.Date).Value = lesson.Zacatek;
            cmd.Parameters.Add("kapacita", OracleDbType.Int32).Value = lesson.Kapacita;
            cmd.Parameters.Add("idtrener", OracleDbType.Int32).Value = 1; // TODO: nahradit ID trenéra (např. z claimu)
            cmd.Parameters.Add("idout", OracleDbType.Int32).Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt32(cmd.Parameters["idout"].Value.ToString());
        }

        public async Task<bool> UpdateAsync(Lesson lesson, CancellationToken ct = default)
        {
            const string sql = @"
                UPDATE lekce
                   SET nazevlekce = :nazev,
                       datumlekce = :datum,
                       obsazenost = :kapacita
                 WHERE idlekce    = :id";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };

            cmd.Parameters.Add("nazev", OracleDbType.Varchar2).Value = lesson.Nazev;
            cmd.Parameters.Add("datum", OracleDbType.Date).Value = lesson.Zacatek;
            cmd.Parameters.Add("kapacita", OracleDbType.Int32).Value = lesson.Kapacita;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = lesson.Id;

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("DELETE FROM lekce WHERE idlekce = :id", con) { BindByName = true };
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }
    }
}
