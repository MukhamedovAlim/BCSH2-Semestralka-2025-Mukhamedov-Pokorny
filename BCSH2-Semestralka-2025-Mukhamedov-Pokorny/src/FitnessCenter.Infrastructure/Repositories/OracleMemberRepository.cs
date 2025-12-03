using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;          // Member
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class OracleMemberRepository : IMembersRepository
    {
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        // ------------------------------------------------------------
        // CLENOVE – mapování z/do entity Member
        // ------------------------------------------------------------
        private static Member ReadMember(OracleDataReader rd)
        {
            // POŘADÍ MUSÍ SEDĚT SE VŠEMI SELECTY:
            // idclen, jmeno, prijmeni, email, adresa, telefon, heslo_hash, musi_zmenit_heslo
            return new Member
            {
                MemberId = rd.GetInt32(0),
                FirstName = rd.GetString(1),
                LastName = rd.GetString(2),
                Email = rd.GetString(3),
                Address = rd.IsDBNull(4) ? null : rd.GetString(4),
                Phone = rd.IsDBNull(5) ? null : rd.GetString(5),
                PasswordHash = rd.IsDBNull(6) ? null : rd.GetString(6),
                MustChangePassword = !rd.IsDBNull(7) && rd.GetInt32(7) == 1
            };
        }

        // ------------------------------------------------------------
        // READ ALL
        // ------------------------------------------------------------
        public async Task<IEnumerable<Member>> GetAllAsync()
        {
            const string sql = @"
                SELECT idclen,
                       jmeno,
                       prijmeni,
                       email,
                       adresa,
                       telefon,
                       heslo_hash,
                       MUSI_ZMENIT_HESLO
                  FROM clenove
              ORDER BY prijmeni, jmeno";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            using var rd = await cmd.ExecuteReaderAsync();

            var list = new List<Member>();
            while (await rd.ReadAsync())
                list.Add(ReadMember(rd));

            return list;
        }

        // ------------------------------------------------------------
        // READ BY ID
        // ------------------------------------------------------------
        public async Task<Member?> GetByIdAsync(int id)
        {
            const string sql = @"
                SELECT idclen,
                       jmeno,
                       prijmeni,
                       email,
                       adresa,
                       telefon,
                       heslo_hash,
                       MUSI_ZMENIT_HESLO
                  FROM clenove
                 WHERE idclen = :id";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", id);

            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
                return ReadMember(rd);

            return null;
        }

        // ------------------------------------------------------------
        // CREATE – přes PR_CLEN_CREATE (admin / registrace)
        // ------------------------------------------------------------
        public async Task<int> CreateAsync(Member m)
        {
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand("PR_CLEN_CREATE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_jmeno", OracleDbType.Varchar2).Value = m.FirstName;
            cmd.Parameters.Add("p_prijmeni", OracleDbType.Varchar2).Value = m.LastName;
            cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = m.Email;
            cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2).Value = (object?)m.Phone ?? DBNull.Value;
            cmd.Parameters.Add("p_datumnarozeni", OracleDbType.Date).Value = m.BirthDate;
            cmd.Parameters.Add("p_adresa", OracleDbType.Varchar2).Value = (object?)m.Address ?? DBNull.Value;
            cmd.Parameters.Add("p_idfitness", OracleDbType.Int32).Value = m.FitnessCenterId;
            cmd.Parameters.Add("p_heslo_hash", OracleDbType.Varchar2).Value =
                (object?)m.PasswordHash ?? DBNull.Value;

            // DŮLEŽITÉ: vynucení změny hesla – PR_CLEN_CREATE má parametr p_musi_zmenit
            cmd.Parameters.Add("p_musi_zmenit", OracleDbType.Int32)
               .Value = m.MustChangePassword ? 1 : 0;

            var pNewId = new OracleParameter("p_new_id", OracleDbType.Int32)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(pNewId);

            await cmd.ExecuteNonQueryAsync();

            return Convert.ToInt32(pNewId.Value.ToString());
        }

        // ------------------------------------------------------------
        // UPDATE (přímé SQL) – základní editace profilu
        // ------------------------------------------------------------
        public async Task<bool> UpdateAsync(Member m)
        {
            const string sql = @"
                UPDATE clenove
                   SET jmeno    = :jmeno,
                       prijmeni = :prijmeni,
                       adresa   = :adresa,
                       telefon  = :telefon,
                       email    = :email
                 WHERE idclen   = :id";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };

            cmd.Parameters.Add("jmeno", m.FirstName ?? string.Empty);
            cmd.Parameters.Add("prijmeni", m.LastName ?? string.Empty);
            cmd.Parameters.Add("adresa", (object?)m.Address ?? DBNull.Value);
            cmd.Parameters.Add("telefon", (object?)m.Phone ?? DBNull.Value);
            cmd.Parameters.Add("email", m.Email ?? string.Empty);
            cmd.Parameters.Add("id", m.MemberId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        // ------------------------------------------------------------
        // ZMĚNA HESLA PŘES PROCEDURU – PR_CLEN_CHANGE_PASSWORD
        // ------------------------------------------------------------
        public async Task ChangePasswordAsync(int memberId, string newHash)
        {
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand("PR_CLEN_CHANGE_PASSWORD", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = memberId;
            cmd.Parameters.Add("p_heslo_hash", OracleDbType.Varchar2).Value = newHash;

            await cmd.ExecuteNonQueryAsync();
        }

        // ------------------------------------------------------------
        // DELETE
        // ------------------------------------------------------------
        public async Task<bool> DeleteAsync(int id)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("DELETE FROM clenove WHERE idclen = :id", con)
            { BindByName = true };
            cmd.Parameters.Add("id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        // ------------------------------------------------------------
        // TRAINER HELPERS (TRENERI)
        // ------------------------------------------------------------
        public async Task<bool> IsTrainerEmailAsync(string email)
        {
            const string sql = @"SELECT COUNT(*) FROM treneri WHERE LOWER(email) = LOWER(:email)";
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("email", email);

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<IEnumerable<Member>> GetAllNonTrainersAsync()
        {
            const string sql = @"
                SELECT c.idclen,
                       c.jmeno,
                       c.prijmeni,
                       c.email,
                       c.adresa,
                       c.telefon,
                       c.heslo_hash,
                       c.MUSI_ZMENIT_HESLO
                  FROM clenove c
                 WHERE NOT EXISTS (
                       SELECT 1
                         FROM treneri t
                        WHERE LOWER(t.email) = LOWER(c.email)
                 )
              ORDER BY c.prijmeni, c.jmeno";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            using var rd = await cmd.ExecuteReaderAsync();

            var list = new List<Member>();
            while (await rd.ReadAsync())
                list.Add(ReadMember(rd));

            return list;
        }

        public async Task<int?> GetTrainerIdByEmailAsync(string email)
        {
            const string sql = @"SELECT idtrener FROM treneri WHERE LOWER(email) = LOWER(:email)";
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("email", email);

            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj == DBNull.Value) return null;
            return Convert.ToInt32(obj);
        }

        // ============================================================
        // VOLÁNÍ PROC – CreateViaProcedureAsync / UpdateViaProcedureAsync
        // (použij jen pokud je opravdu potřebuješ zvlášť)
        // ============================================================

        public async Task<int> CreateViaProcedureAsync(Member m)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("PR_CLEN_CREATE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            if (m.BirthDate == default)
                throw new ArgumentNullException(nameof(m.BirthDate));

            if (m.FitnessCenterId <= 0)
                throw new ArgumentNullException(nameof(m.FitnessCenterId));

            object OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? DBNull.Value : v.Trim();

            cmd.Parameters.Add("p_jmeno", OracleDbType.Varchar2).Value = OrNull(m.FirstName);
            cmd.Parameters.Add("p_prijmeni", OracleDbType.Varchar2).Value = OrNull(m.LastName);
            cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = OrNull(m.Email);
            cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2).Value = OrNull(m.Phone);
            cmd.Parameters.Add("p_datumnarozeni", OracleDbType.Date).Value = m.BirthDate;
            cmd.Parameters.Add("p_adresa", OracleDbType.Varchar2).Value = OrNull(m.Address);
            cmd.Parameters.Add("p_idfitness", OracleDbType.Int32).Value = m.FitnessCenterId;
            cmd.Parameters.Add("p_heslo_hash", OracleDbType.Varchar2).Value =
                (object?)m.PasswordHash ?? DBNull.Value;

            // 🔥 TADY JE TEN PODSTATNÝ ŘÁDEK:
            cmd.Parameters.Add("p_musi_zmenit", OracleDbType.Int32).Value =
                m.MustChangePassword ? 1 : 0;

            var pOut = new OracleParameter("p_new_id", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add(pOut);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                return Convert.ToInt32(pOut.Value.ToString());
            }
            catch (OracleException ex) when (ex.Number == 1) // ORA-00001 (UNIQUE)
            {
                throw new InvalidOperationException("Duplicitní e-mail nebo telefon.", ex);
            }
        }


        public async Task UpdateViaProcedureAsync(Member m)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand("PR_CLEN_UPDATE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            if (m.BirthDate == default)
                throw new ArgumentNullException(nameof(m.BirthDate));

            if (m.FitnessCenterId <= 0)
                throw new ArgumentNullException(nameof(m.FitnessCenterId));

            object OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? DBNull.Value : v.Trim();

            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = m.MemberId;
            cmd.Parameters.Add("p_jmeno", OracleDbType.Varchar2).Value = OrNull(m.FirstName);
            cmd.Parameters.Add("p_prijmeni", OracleDbType.Varchar2).Value = OrNull(m.LastName);
            cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = OrNull(m.Email);
            cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2).Value = OrNull(m.Phone);
            cmd.Parameters.Add("p_datumnarozeni", OracleDbType.Date).Value = m.BirthDate;
            cmd.Parameters.Add("p_adresa", OracleDbType.Varchar2).Value = OrNull(m.Address);
            cmd.Parameters.Add("p_idfitness", OracleDbType.Int32).Value =
                (m.FitnessCenterId > 0) ? m.FitnessCenterId : (object)DBNull.Value;

            // p_musi_zmenit NEPOSÍLÁME → v proceduře zůstane stávající hodnota

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
