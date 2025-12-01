using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using FitnessCenter.Infrastructure.Persistence;   // DatabaseManager
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class AdminLogsRepository : IAdminLogsRepository
    {
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        public async Task<IReadOnlyList<LogRow>> GetLogsAsync(int top = 3000)
        {
            const string sql = @"
              SELECT * FROM (
                SELECT KDY, TABULKA, OPERACE, KDO, NVL(POPIS,'')
                  FROM LOG_OPERACE
                 ORDER BY KDY DESC
              ) WHERE ROWNUM <= :top";

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("top", OracleDbType.Int32).Value = top;

            var list = new List<LogRow>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new LogRow(
                    rd.GetDateTime(0),
                    rd.GetString(1),
                    rd.GetString(2),
                    rd.GetString(3),
                    rd.GetString(4)
                ));
            return list;
        }
    }
}
