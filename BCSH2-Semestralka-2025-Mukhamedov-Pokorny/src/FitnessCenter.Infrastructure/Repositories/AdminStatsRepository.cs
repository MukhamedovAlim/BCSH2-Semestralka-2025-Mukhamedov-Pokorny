using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;
using System.Data;

public sealed class AdminStatsRepository
{
    private static async Task<OracleConnection> OpenAsync()
        => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

    // f_pocet_aktivnich_clenu(p_idfitness) -> INT (parsujeme z textu)
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

    // najdeme nějakého člena ve vybraném fitku
    public async Task<int?> GetAnyMemberIdInFitnessAsync(int fitkoId)
    {
        using var con = await OpenAsync();
        using var cmd = new OracleCommand(
            @"SELECT idclen 
                FROM clenove 
               WHERE fitnesscentrum_idfitness = :fitko
            FETCH FIRST 1 ROWS ONLY", con)
        { BindByName = true, CommandType = CommandType.Text };

        cmd.Parameters.Add("fitko", OracleDbType.Int32).Value = fitkoId;

        var v = await cmd.ExecuteScalarAsync();
        return v == null ? (int?)null : Convert.ToInt32(v);
    }

    // f_hodnoceni_clena(p_idclen) -> VARCHAR2 (text)
    public async Task<string> GetMemberRatingAsync(int memberId)
    {
        using var con = await OpenAsync();
        using var cmd = new OracleCommand("SELECT f_hodnoceni_clena(:id) FROM dual", con)
        { BindByName = true, CommandType = CommandType.Text };

        cmd.Parameters.Add("id", OracleDbType.Int32).Value = memberId;

        var val = await cmd.ExecuteScalarAsync();
        return Convert.ToString(val) ?? "—";
    }
}
