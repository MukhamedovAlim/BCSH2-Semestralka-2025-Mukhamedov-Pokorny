using FitnessCenter.Infrastructure.DBObjects;
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class DbCatalogRepository
    {
        // stejný styl jako ostatní repozitáře
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        // ========== SEZNAM OBJEKTŮ ===========================================
        public async Task<IReadOnlyList<DbObjectRow>> GetObjectsAsync()
        {
            const string sql = @"
                SELECT object_type, object_name, created, last_ddl_time
                FROM   V_DB_OBJECTS
                ORDER BY object_type, object_name";

            var result = new List<DbObjectRow>();

            using var conn = await OpenAsync();
            using var cmd = new OracleCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

            while (await reader.ReadAsync())
            {
                result.Add(new DbObjectRow
                {
                    ObjectType = reader.GetString(0),
                    ObjectName = reader.GetString(1),
                    Created = reader.GetDateTime(2),
                    LastDdlTime = reader.GetDateTime(3)
                });
            }

            return result;
        }

        // ========== DETAIL OBJEKTU ==========================================
        public async Task<DbObjectDetail?> GetDetailAsync(string objectType, string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectType))
                throw new ArgumentNullException(nameof(objectType));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));

            objectType = objectType.ToUpperInvariant();
            objectName = objectName.ToUpperInvariant();

            var detail = new DbObjectDetail
            {
                ObjectType = objectType,
                ObjectName = objectName
            };

            using var conn = await OpenAsync();

            switch (objectType)
            {
                case "TABLE":
                    // struktura sloupců
                    detail.Columns = await LoadTableColumnsAsync(conn, objectName);
                    // DDL – CREATE TABLE ...
                    detail.DefinitionText = await LoadTableDdlAsync(conn, objectName);
                    break;

                case "SEQUENCE":
                    detail.Sequence = await LoadSequenceInfoAsync(conn, objectName);
                    break;

                case "VIEW":
                    detail.DefinitionText = await LoadViewDdlAsync(conn, objectName);
                    break;

                case "FUNCTION":
                case "PROCEDURE":
                case "TRIGGER":
                    detail.DefinitionText = await LoadSourceTextAsync(conn, objectType, objectName);
                    break;

                default:
                    detail.DefinitionText = "Detail pro tento typ objektu není implementován.";
                    break;
            }

            return detail;
        }

        // ---------- pomocné metody ------------------------------------------
        private static async Task<List<TableColumnInfo>> LoadTableColumnsAsync(
            OracleConnection conn, string tableName)
        {
            const string sql = @"
                SELECT column_id, column_name, data_type, data_length, nullable
                FROM   user_tab_columns
                WHERE  table_name = :name
                ORDER BY column_id";

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = tableName;

            var list = new List<TableColumnInfo>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TableColumnInfo
                {
                    ColumnId = reader.GetInt32(0),
                    ColumnName = reader.GetString(1),
                    DataType = reader.GetString(2),
                    DataLength = Convert.ToInt32(reader["DATA_LENGTH"]),
                    IsNullable = string.Equals(reader.GetString(4), "Y", StringComparison.OrdinalIgnoreCase)
                });
            }

            return list;
        }

        private static async Task<string?> LoadTableDdlAsync(OracleConnection conn, string tableName)
        {
            // 1) nastavení transform parametrů pro aktuální session
            const string transformSql = @"
BEGIN
  DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'SEGMENT_ATTRIBUTES', FALSE);
  DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'STORAGE',           FALSE);
  DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'TABLESPACE',        FALSE);
  DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM, 'REF_CONSTRAINTS',   TRUE);
END;";

            using (var transformCmd = new OracleCommand(transformSql, conn))
            {
                await transformCmd.ExecuteNonQueryAsync();
            }

            // 2) samotné DDL – už „osekané“ od STORAGE/TABLESPACE
            const string ddlSql = @"SELECT DBMS_METADATA.GET_DDL('TABLE', :name) FROM dual";

            using var cmd = new OracleCommand(ddlSql, conn) { BindByName = true };
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = tableName.ToUpperInvariant();

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.IsDBNull(0))
                return null;

            var value = reader.GetValue(0);

            if (value is string s)
                return s;

            if (value is OracleClob clob)
                return clob.IsNull ? null : clob.Value;

            return value?.ToString();
        }

        private static async Task<SequenceInfo?> LoadSequenceInfoAsync(
            OracleConnection conn, string sequenceName)
        {
            const string sql = @"
                SELECT sequence_name, min_value, max_value, increment_by, last_number
                FROM   user_sequences
                WHERE  sequence_name = :name";

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = sequenceName;

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new SequenceInfo
            {
                SequenceName = reader.GetString(0),
                MinValue = reader.GetDecimal(1),
                MaxValue = reader.GetDecimal(2),
                IncrementBy = reader.GetDecimal(3),
                LastNumber = reader.GetDecimal(4)
            };
        }

        private static async Task<string?> LoadViewDdlAsync(OracleConnection conn, string viewName)
        {
            const string sql = @"SELECT DBMS_METADATA.GET_DDL('VIEW', :name) FROM dual";

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = viewName.ToUpperInvariant();

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.IsDBNull(0))
                return null;

            var value = reader.GetValue(0);

            if (value is string s)
                return s;

            if (value is OracleClob clob)
                return clob.IsNull ? null : clob.Value;

            return value?.ToString();
        }


        private static async Task<string?> LoadSourceTextAsync(
            OracleConnection conn, string type, string name)
        {
            const string sql = @"
                SELECT text
                FROM   user_source
                WHERE  name = :name
                AND    type = :type
                ORDER BY line";

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = name;
            cmd.Parameters.Add("type", OracleDbType.Varchar2).Value = type;

            var sb = new StringBuilder();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sb.Append(reader.GetString(0));
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
