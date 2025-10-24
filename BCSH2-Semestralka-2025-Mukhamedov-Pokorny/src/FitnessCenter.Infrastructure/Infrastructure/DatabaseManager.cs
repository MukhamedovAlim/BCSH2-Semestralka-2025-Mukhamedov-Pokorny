using Oracle.ManagedDataAccess.Client;
using System.Threading.Tasks;

namespace FitnessCenter.Infrastructure.Persistence
{
    public static class DatabaseManager
    {
        private static readonly string connectionString =
            "User Id=st72870;Password=HESLO;" +
            "Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=fei-sql3.upceucebny.cz)(PORT=1521)))(CONNECT_DATA=(SID=BDAS)))";
        // pokud by to nešlo, zkus (SERVICE_NAME=BDAS)

        public static OracleConnection GetConnection() => new OracleConnection(connectionString);
        public static async Task<OracleConnection> GetOpenConnectionAsync()
        {
            var c = new OracleConnection(connectionString);
            await c.OpenAsync();
            return c;
        }
    }
}
