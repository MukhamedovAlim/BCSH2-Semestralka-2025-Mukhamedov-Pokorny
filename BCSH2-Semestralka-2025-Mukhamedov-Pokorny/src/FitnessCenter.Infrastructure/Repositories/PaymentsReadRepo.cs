using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using FitnessCenter.Infrastructure.Persistence;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class PaymentsReadRepo
    {
        // Otevření připojení (z DatabaseManager)
        private static async Task<OracleConnection> OpenAsync()
        {
            return (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
        }

        // 1️⃣ Zjištění ID člena podle e-mailu
        public async Task<int?> GetMemberIdByEmailAsync(string email)
        {
            const string sql = @"SELECT idclen FROM clenove WHERE LOWER(email) = LOWER(:email)";
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("email", email);

            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj is DBNull) return null;
            return Convert.ToInt32(obj);
        }

        // 2️⃣ Struktura pro historii plateb
        public sealed record PaymentRow(DateTime Datum, string Popis, decimal Castka, string Stav);

        // Získání historie plateb člena (z view v_platby_hist)
        public async Task<List<PaymentRow>> GetPaymentsAsync(int clenId)
        {
            const string sql = @"
                SELECT datumplatby, popis, castka, stav
                FROM v_platby_hist
                WHERE id_clena = :id
                ORDER BY datumplatby DESC";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", clenId);

            var list = new List<PaymentRow>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new PaymentRow(
                    rd.GetDateTime(0),
                    rd.GetString(1),
                    rd.GetDecimal(2),
                    rd.GetString(3)
                ));
            }
            return list;
        }

        // 3️⃣ Struktura pro stav členství
        public sealed record MembershipStatus(bool Active, string? TypeName, DateTime? From, DateTime? To, int DaysLeft);

        // Vrací aktuální členství (z view v_clen_stav_perm)
        public async Task<MembershipStatus> GetMembershipAsync(int clenId)
        {
            const string sql = @"
                SELECT * FROM (
                  SELECT zahajeni, ukonceni, typ_permanentky, aktivni, dnu_zbyva
                  FROM v_clen_stav_perm
                  WHERE id_clena = :id
                  ORDER BY ukonceni DESC
                ) WHERE ROWNUM = 1";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", clenId);

            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                var active = rd.GetInt32(rd.GetOrdinal("aktivni")) == 1;
                var from = rd.GetDateTime(rd.GetOrdinal("zahajeni"));
                var to = rd.GetDateTime(rd.GetOrdinal("ukonceni"));
                var type = rd.GetString(rd.GetOrdinal("typ_permanentky"));
                var left = Convert.ToInt32(rd.GetDecimal(rd.GetOrdinal("dnu_zbyva")));
                return new MembershipStatus(active, type, from, to, left);
            }

            return new MembershipStatus(false, null, null, null, 0);
        }

        // 4️⃣ Volání PL/SQL procedury pro nákup členství
        public async Task<(int IdClenstvi, int IdPlatba)> PurchaseMembershipAsync(string email, string typNazev, decimal cena)
        {
            using var con = await OpenAsync();
            using var tx = con.BeginTransaction();
            using var cmd = new OracleCommand("prodej_clenstvi_existujicimu", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true,
                Transaction = tx
            };

            cmd.Parameters.Add("p_email", email);
            cmd.Parameters.Add("p_typ_nazev", typNazev);
            cmd.Parameters.Add("p_castka", cena);
            cmd.Parameters.Add("p_idclenstvi_out", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add("p_idplatba_out", OracleDbType.Int32, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            tx.Commit();

            int idClenstvi = Convert.ToInt32(cmd.Parameters["p_idclenstvi_out"].Value.ToString());
            int idPlatba = Convert.ToInt32(cmd.Parameters["p_idplatba_out"].Value.ToString());
            return (idClenstvi, idPlatba);
        }

        // 5️⃣ 🆕 Detail konkrétní platby (pro stránku Success)
        public sealed record PaymentDetailRow(int IdPlatby, DateTime Datum, string Popis, decimal Castka, string Stav, string? Reference);

        public async Task<PaymentDetailRow?> GetPaymentByIdAsync(int idPlatby, string email)
        {
            const string sql = @"
                SELECT p.idplatby,
                       p.datumplatby,
                       p.popis,
                       p.castka,
                       s.stavplatby,
                       NULL AS reference
                  FROM platby p
                  JOIN stavyplateb s ON s.idstavplatby = p.stavplatby_idstavplatby
                 WHERE p.idplatby = :id
                   AND EXISTS (
                         SELECT 1
                           FROM clenove c
                          WHERE c.idclen = p.clen_idclena
                            AND LOWER(c.email) = LOWER(:email)
                     )";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", idPlatby);
            cmd.Parameters.Add("email", email);

            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                return new PaymentDetailRow(
                    rd.GetInt32(0),
                    rd.GetDateTime(1),
                    rd.IsDBNull(2) ? "" : rd.GetString(2),
                    rd.GetDecimal(3),
                    rd.GetString(4),
                    rd.IsDBNull(5) ? null : rd.GetString(5)
                );
            }
            return null;
        }
    }
}
