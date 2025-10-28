using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Infrastructure.Repositories;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

public sealed class TrainersReadRepo : ITrainersReadRepo
{
    public async Task<int?> GetTrenerIdByEmailAsync(string email)
    {
        using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
        using var cmd = new OracleCommand(
            "SELECT idtrener FROM treneri WHERE LOWER(email) = LOWER(:eml)", con)
        { BindByName = true };
        cmd.Parameters.Add("eml", OracleDbType.Varchar2).Value = email;

        var obj = await cmd.ExecuteScalarAsync();
        return obj == null || obj == DBNull.Value ? null : Convert.ToInt32(obj);
    }
}
