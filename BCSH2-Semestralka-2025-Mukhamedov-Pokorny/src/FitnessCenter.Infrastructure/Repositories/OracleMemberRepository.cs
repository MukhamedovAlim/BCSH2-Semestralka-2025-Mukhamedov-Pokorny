using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;          // Member
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Infrastructure.Repositories;
using Oracle.ManagedDataAccess.Client;

public sealed class OracleMemberRepository : IMembersRepository
{
    private static async Task<OracleConnection> OpenAsync()
        => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

    // ------------------------------------------------------------
    // CLENOVE – mapování z/do entity Member
    // ------------------------------------------------------------
    private static Member ReadMember(OracleDataReader rd)
    {
        // Sloupce v SELECTech níže musí odpovídat pořadí zde:
        // idclen, jmeno, prijmeni, email, adresa, telefon
        return new Member
        {
            MemberId = rd.GetInt32(0),
            FirstName = rd.GetString(1),
            LastName = rd.GetString(2),
            Email = rd.GetString(3),
            Address = rd.IsDBNull(4) ? null : rd.GetString(4),
            Phone = rd.IsDBNull(5) ? null : rd.GetString(5),
            // BirthDate a FitnessCenterId nečtu, protože nejsou ve SELECTu
        };
    }

    // ------------------------------------------------------------
    // READ ALL
    // ------------------------------------------------------------
    public async Task<IEnumerable<Member>> GetAllAsync()
    {
        const string sql = @"
            SELECT idclen, jmeno, prijmeni, email, adresa, telefon
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
            SELECT idclen, jmeno, prijmeni, email, adresa, telefon
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
    // CREATE (přímé SQL) – ponecháváme kvůli kompatibilitě
    //  - ID ze sekvence S_CLENOVE
    //  - DATUMNAROZENI nastaví SYSDATE (v DB NOT NULL)
    //  - FITNESSCENTRUM_IDFITNESS vezme MIN(idfitness) nebo 1
    // ------------------------------------------------------------
    public async Task<int> CreateAsync(Member m)
    {
        const string sql = @"
            INSERT INTO clenove
                (idclen, jmeno, prijmeni, datumnarozeni, adresa, telefon, email, fitnesscentrum_idfitness)
            VALUES
                (S_CLENOVE.NEXTVAL, :jmeno, :prijmeni, SYSDATE, :adresa, :telefon, :email,
                 (SELECT NVL(MIN(idfitness), 1) FROM fitnesscentra))
            RETURNING idclen INTO :idout";

        using var con = await OpenAsync();
        using var cmd = new OracleCommand(sql, con) { BindByName = true };

        cmd.Parameters.Add("jmeno", m.FirstName ?? string.Empty);
        cmd.Parameters.Add("prijmeni", m.LastName ?? string.Empty);
        cmd.Parameters.Add("adresa", (object?)m.Address ?? DBNull.Value);
        cmd.Parameters.Add("telefon", (object?)m.Phone ?? DBNull.Value);
        cmd.Parameters.Add("email", m.Email ?? string.Empty);

        cmd.Parameters.Add("idout", OracleDbType.Int32).Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();
        return Convert.ToInt32(cmd.Parameters["idout"].Value.ToString());
    }

    // ------------------------------------------------------------
    // UPDATE (přímé SQL) – ponecháváme kvůli kompatibilitě
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
    // NOVÉ: volání uložených procedur (BDAS2 #12)
    // ============================================================

    // Create přes PR_CLEN_CREATE
    public async Task<int> CreateViaProcedureAsync(Member m)
    {
        using var con = await OpenAsync();
        using var cmd = new OracleCommand("PR_CLEN_CREATE", con)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        // před Add parametrů
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
        cmd.Parameters.Add("p_idfitness", OracleDbType.Int32).Value = (m.FitnessCenterId > 0) ? m.FitnessCenterId : throw new ArgumentNullException(nameof(m.FitnessCenterId));

        var pOut = new OracleParameter("p_new_id", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(pOut);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(pOut.Value.ToString());
        }
        catch (OracleException ex) when (ex.Number == 1) // ORA-00001 (UNIQUE: email/telefon)
        {
            throw new InvalidOperationException("Duplicitní e-mail nebo telefon.", ex);
        }
    }

    // Update přes PR_CLEN_UPDATE
    public async Task UpdateViaProcedureAsync(Member m)
    {
        using var con = await OpenAsync();
        using var cmd = new OracleCommand("PR_CLEN_UPDATE", con)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        // před Add parametrů
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
        cmd.Parameters.Add("p_idfitness", OracleDbType.Int32).Value = (m.FitnessCenterId > 0) ? m.FitnessCenterId : (object)DBNull.Value;

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (ex.Number == 1) // ORA-00001 (UNIQUE)
        {
            throw new InvalidOperationException("Duplicitní e-mail nebo telefon.", ex);
        }
    }
}
