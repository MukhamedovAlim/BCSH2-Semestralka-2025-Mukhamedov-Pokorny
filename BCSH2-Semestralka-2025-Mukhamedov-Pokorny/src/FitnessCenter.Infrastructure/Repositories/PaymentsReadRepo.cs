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
                    rd.IsDBNull(1) ? "" : rd.GetString(1),
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

        // 4️⃣ Detail konkrétní platby (pro stránku Success)
        public sealed record PaymentDetailRow(
            int IdPlatby,
            DateTime Datum,
            string Popis,
            decimal Castka,
            string Stav,
            string? Reference
        );

        public async Task<PaymentDetailRow?> GetPaymentByIdAsync(int idPlatby, string email)
        {
            const string sql = @"
        SELECT p.idplatby,
               p.datumplatby,
               p.castka,
               s.stavplatby,
               NULL AS reference
          FROM platby p
          JOIN stavyplateb s ON s.idstavplatby = p.stavplatby_idstavplatby
         WHERE p.idplatba = :id
           AND EXISTS (
                 SELECT 1
                   FROM clenove c
                  WHERE c.idclen = p.clen_idclen
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
                    IdPlatby: rd.GetInt32(0),
                    Datum: rd.GetDateTime(1),
                    Popis: "", // žádný popis z PLATBY, DB ho nemá
                    Castka: rd.GetDecimal(2),
                    Stav: rd.GetString(3),
                    Reference: rd.IsDBNull(4) ? null : rd.GetString(4)
                );
            }
            return null;
        }


        // 5️⃣ Admin – platby ve stavu „Vyřizuje se“
        public sealed record AdminPaymentRow(
    int IdPlatba,
    int MemberId,
    string MemberName,
    string Email,
    DateTime Datum,
    decimal Castka,
    string Stav
);


        public async Task<List<AdminPaymentRow>> GetPendingPaymentsAsync()
        {
            const string sql = @"
        SELECT
            p.idplatba,
            c.idclen,
            c.jmeno || ' ' || c.prijmeni AS member_name,
            c.email,
            p.datumplatby,
            p.castka,
            s.stavplatby
        FROM platby p
        JOIN clenove c
          ON c.idclen = p.clen_idclen
        JOIN stavyplateb s
          ON s.idstavplatby = p.stavplatby_idstavplatby
        WHERE UPPER(s.stavplatby) = 'VYŘIZUJE SE'
        ORDER BY p.datumplatby DESC";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };

            var list = new List<AdminPaymentRow>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new AdminPaymentRow(
                    IdPlatba: rd.GetInt32(0),
                    MemberId: rd.GetInt32(1),
                    MemberName: rd.GetString(2),
                    Email: rd.GetString(3),
                    Datum: rd.GetDateTime(4),
                    Castka: rd.GetDecimal(5),
                    Stav: rd.GetString(6)
                ));
            }
            return list;
        }

    }
}
