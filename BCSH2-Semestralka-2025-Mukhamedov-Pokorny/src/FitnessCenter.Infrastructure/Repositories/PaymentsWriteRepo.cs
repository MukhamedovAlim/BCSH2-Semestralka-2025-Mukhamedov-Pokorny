using System;
using System.Data;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Infrastructure.Repositories
{
    public class PaymentsWriteRepo
    {
        public async Task<int> CreatePaymentAsync(
            int memberId,
            decimal amount,
            string stavNazev = "Vyřizuje se",
            DateTime? datum = null
        )
        {
            if (amount <= 0)
                throw new ArgumentException("Částka musí být kladná.", nameof(amount));

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "vytvorit_platbu";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;

            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = memberId;
            cmd.Parameters.Add("p_castka", OracleDbType.Decimal).Value = amount;
            cmd.Parameters.Add("p_stav_nazev", OracleDbType.Varchar2).Value = stavNazev;
            cmd.Parameters.Add("p_datum", OracleDbType.Date).Value = (object?)datum ?? DBNull.Value;

            var outParam = new OracleParameter("p_idplatba_out", OracleDbType.Int32)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outParam);

            await cmd.ExecuteNonQueryAsync();

            return Convert.ToInt32(outParam.Value.ToString());
        }

        public async Task ApproveMembershipPaymentAsync(int idPlatba)
        {
            using var conn = await DatabaseManager.GetOpenConnectionAsync();

            // 1) Zjistit částku
            decimal amount;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT castka FROM platby WHERE idplatba = :id";
                cmd.BindByName = true;
                cmd.Parameters.Add("id", OracleDbType.Int32).Value = idPlatba;

                var o = await cmd.ExecuteScalarAsync();
                if (o == null)
                    throw new InvalidOperationException("Platba neexistuje.");

                amount = Convert.ToDecimal(o);
            }

            // 2) Odvodit typ podle částky
            string typ =
                amount == 150m ? "Jednorázové" :
                amount == 990m ? "Měsíční" :
                amount == 7990m ? "Roční" :
                throw new InvalidOperationException("Neznámá částka – nelze určit typ členství.");

            // 3) Zavolat proceduru
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "APPROVE_MEMBERSHIP_PAYMENT";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                cmd.Parameters.Add("p_idplatby", OracleDbType.Int32).Value = idPlatba;
                cmd.Parameters.Add("p_typ_nazev", OracleDbType.Varchar2).Value = typ;

                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task RejectMembershipPaymentAsync(int idPlatba)
        {
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "REJECT_MEMBERSHIP_PAYMENT";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;

            cmd.Parameters.Add("p_idplatby", OracleDbType.Int32).Value = idPlatba;

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
