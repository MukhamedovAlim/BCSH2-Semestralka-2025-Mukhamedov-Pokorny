using Oracle.ManagedDataAccess.Client;
using System.Threading.Tasks;

namespace FitnessCenter.Infrastructure.Persistence
{
    public static class DatabaseManager
    {
        // ← dosaď SVÉ přihlašovací údaje
        private const string User = "st72562";
        private const string Password = "david2004";

        private static string ConnString =>
            $"User Id={User};Password={Password};" +
            "Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=fei-sql3.upceucebny.cz)(PORT=1521)))(CONNECT_DATA=(SID=BDAS)))";

        public static OracleConnection GetConnection() => new OracleConnection(ConnString);

        public static async Task<OracleConnection> GetOpenConnectionAsync()
        {
            OracleConnection.ClearAllPools();
            var c = new OracleConnection(ConnString);
            await c.OpenAsync();
            return c;
        }
    }
}
