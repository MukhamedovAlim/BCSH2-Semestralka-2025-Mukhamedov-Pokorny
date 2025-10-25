using System.Data;
using Oracle.ManagedDataAccess.Client;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Domain.Entities;
using Oracle.ManagedDataAccess.Types; // kvůli OracleDecimal

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class OracleMembersRepository : IMembersRepository
    {
        public async Task<IEnumerable<Member>> GetAllAsync(CancellationToken ct = default)
        {
            var list = new List<Member>();

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                @"SELECT IDCLEN, JMENO, PRIJMENI, EMAIL 
                    FROM CLENOVE 
                ORDER BY IDCLEN", conn);

            using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                list.Add(new Member
                {
                    MemberId = rd.GetInt32(0),
                    FirstName = rd.GetString(1),
                    LastName = rd.GetString(2),
                    Email = rd.GetString(3)
                });
            }
            return list;
        }

        public async Task<Member?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                @"SELECT IDCLEN, JMENO, PRIJMENI, EMAIL 
                    FROM CLENOVE 
                   WHERE IDCLEN = :id", conn)
            { BindByName = true };

            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;

            using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                return new Member
                {
                    MemberId = rd.GetInt32(0),
                    FirstName = rd.GetString(1),
                    LastName = rd.GetString(2),
                    Email = rd.GetString(3)
                };
            }
            return null;
        }

        public async Task<int> CreateAsync(Member m, CancellationToken ct = default)
        {
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

            // 1) Získáme nějaké existující FITNESSCENTRUM_IDFITNESS (první řádek).
            int fitnessId;
            using (var pick = new OracleCommand(
                "SELECT IDFITNESS FROM FITNESSCENTRA WHERE ROWNUM = 1", conn))
            {
                pick.Transaction = tx;
                var obj = await pick.ExecuteScalarAsync(ct);
                if (obj == null || obj == DBNull.Value)
                    throw new InvalidOperationException("Tabulka FITNESSCENTRA je prázdná – vlož nejdřív alespoň 1 fitness centrum.");
                fitnessId = Convert.ToInt32(obj);
            }

            // 2) INSERT všech povinných sloupců.
            //    - IDCLEN ze sekvence S_CLENOVE
            //    - DATUMNAROZENI zatím SYSDATE (později nahradíme parametrem z registrace)
            const string sql = @"
                INSERT INTO CLENOVE (
                    IDCLEN, JMENO, PRIJMENI, DATUMNAROZENI, ADRESA, TELEFON, EMAIL, FITNESSCENTRUM_IDFITNESS
                ) VALUES (
                    S_CLENOVE.NEXTVAL, :j, :p, SYSDATE, :adr, :tel, :em, :fc
                )
                RETURNING IDCLEN INTO :id";

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Transaction = tx;

            cmd.Parameters.Add(":j", OracleDbType.Varchar2).Value = m.FirstName;
            cmd.Parameters.Add(":p", OracleDbType.Varchar2).Value = m.LastName;
            cmd.Parameters.Add(":adr", OracleDbType.Varchar2).Value = (object?)m.Address ?? DBNull.Value;
            cmd.Parameters.Add(":tel", OracleDbType.Varchar2).Value = (object?)m.Phone ?? DBNull.Value;
            cmd.Parameters.Add(":em", OracleDbType.Varchar2).Value = m.Email;
            cmd.Parameters.Add(":fc", OracleDbType.Int32).Value = fitnessId;

            var idParam = new OracleParameter(":id", OracleDbType.Int32)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(idParam);

            await cmd.ExecuteNonQueryAsync(ct);
            tx.Commit();

            int id = idParam.Value is OracleDecimal od ? od.ToInt32() : Convert.ToInt32(idParam.Value);
            m.MemberId = id;
            m.CreatedAt = DateTime.UtcNow;
            return id;
        }

        public async Task<bool> UpdateAsync(Member m, CancellationToken ct = default)
        {
            const string sql = @"
                UPDATE CLENOVE
                   SET JMENO = :j, PRIJMENI = :p, EMAIL = :e, ADRESA = :a, TELEFON = :t
                 WHERE IDCLEN = :id";

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };

            cmd.Parameters.Add(":j", OracleDbType.Varchar2).Value = m.FirstName;
            cmd.Parameters.Add(":p", OracleDbType.Varchar2).Value = m.LastName;
            cmd.Parameters.Add(":e", OracleDbType.Varchar2).Value = m.Email;
            cmd.Parameters.Add(":a", OracleDbType.Varchar2).Value = (object?)m.Address ?? DBNull.Value;
            cmd.Parameters.Add(":t", OracleDbType.Varchar2).Value = (object?)m.Phone ?? DBNull.Value;
            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = m.MemberId;

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand("DELETE FROM CLENOVE WHERE IDCLEN = :id", conn)
            { BindByName = true };

            cmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }
    }
}
