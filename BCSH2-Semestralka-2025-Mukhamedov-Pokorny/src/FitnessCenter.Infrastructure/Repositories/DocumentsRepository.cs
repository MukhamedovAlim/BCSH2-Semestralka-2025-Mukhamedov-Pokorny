using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;

public sealed class DocumentsRepository : IDocumentsRepository
{
    public async Task<int> InsertMemberDocumentAsync(
        IFormFile file,
        int memberId,
        string uploadedBy)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        try
        {
            // 🔗 Stejně jako v PaymentsWriteRepo
            await using var conn = await DatabaseManager.GetOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "dok_vlozit";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;

            // soubor -> byte[]
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var fileName = file.FileName;
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;
            var ext = Path.GetExtension(fileName)?.TrimStart('.') ?? string.Empty;

            cmd.Parameters.Add("p_nazev", OracleDbType.Varchar2, fileName, ParameterDirection.Input);
            cmd.Parameters.Add("p_typ", OracleDbType.Varchar2, contentType, ParameterDirection.Input);
            cmd.Parameters.Add("p_pripona", OracleDbType.Varchar2, ext, ParameterDirection.Input);
            cmd.Parameters.Add("p_obsah", OracleDbType.Blob, bytes, ParameterDirection.Input);
            cmd.Parameters.Add("p_kdo", OracleDbType.Varchar2, uploadedBy, ParameterDirection.Input);
            cmd.Parameters.Add("p_idclen", OracleDbType.Int32, memberId, ParameterDirection.Input);

            var pIdOut = new OracleParameter("p_id_out", OracleDbType.Int32)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(pIdOut);

            await cmd.ExecuteNonQueryAsync();

            return Convert.ToInt32(pIdOut.Value.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("==== CHYBA V DocumentsRepository.InsertMemberDocumentAsync ====");
            Console.WriteLine(ex);
            throw;
        }
    }
}
