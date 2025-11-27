using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

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
            await using var conn = await DatabaseManager.GetOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "dok_vlozit";          // přesně jak se jmenuje procedura
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

            cmd.Parameters.Add("p_nazev", OracleDbType.Varchar2).Value = fileName;
            cmd.Parameters.Add("p_typ", OracleDbType.Varchar2).Value = contentType;
            cmd.Parameters.Add("p_pripona", OracleDbType.Varchar2).Value = ext;
            cmd.Parameters.Add("p_obsah", OracleDbType.Blob).Value = bytes;
            cmd.Parameters.Add("p_kdo", OracleDbType.Varchar2).Value = uploadedBy;
            cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = memberId;

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

    // Přehled všech dokumentů pro admina
    public async Task<IReadOnlyList<MemberDocumentInfo>> GetAllMemberDocumentsAsync()
    {
        var result = new List<MemberDocumentInfo>();

        await using var conn = await DatabaseManager.GetOpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
        SELECT  d.id_dok,
                d.nazev,
                d.typ_mime,
                d.pripona,
                d.nahrano_kdy,
                d.nahrano_kym,
                c.idclen,
                c.jmeno,
                c.prijmeni
        FROM    dokumenty d
        JOIN    clenove c ON c.idclen = d.clen_idclen
        ORDER BY d.nahrano_kdy DESC";

        cmd.BindByName = true;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var item = new MemberDocumentInfo
            {
                Id = reader.GetInt32(0),                       // ID_DOK
                FileName = reader.GetString(1),                      // NAZEV
                ContentType = reader.GetString(2),                      // TYP_MIME
                Extension = reader.IsDBNull(3) ? string.Empty
                                                 : reader.GetString(3),// PRIPONA
                UploadedAt = reader.GetDateTime(4),                    // NAHRANO_KDY
                UploadedBy = reader.GetString(5),                      // NAHRANO_KYM
                MemberId = reader.GetInt32(6),                       // IDCLEN
                                                                     // tady spojíme jméno + příjmení do jedné vlastnosti
                MemberName = $"{reader.GetString(7)} {reader.GetString(8)}"
            };

            result.Add(item);
        }

        return result;
    }

    // Stažení konkrétního dokumentu (BLOB) pro File() v controlleru
    public async Task<MemberDocumentContent?> GetDocumentContentAsync(int documentId)
    {
        await using var conn = await DatabaseManager.GetOpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
        SELECT  d.obsah,
                d.nazev,
                d.typ_mime,
                d.pripona
        FROM    dokumenty d
        WHERE   d.id_dok = :id";
        cmd.BindByName = true;
        cmd.Parameters.Add("id", OracleDbType.Int32).Value = documentId;

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

        if (!await reader.ReadAsync())
            return null;

        var blob = reader.GetOracleBlob(0);
        var bytes = blob?.Value ?? Array.Empty<byte>();

        return new MemberDocumentContent
        {
            Bytes = bytes,
            FileName = reader.GetString(1),
            ContentType = reader.GetString(2),
            Extension = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
        };
    }

    //  Smazání dokumentu
    public async Task DeleteDocumentAsync(int documentId)
    {
        try
        {
            await using var conn = await DatabaseManager.GetOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "dok_smazat";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;

            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = documentId;

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("==== CHYBA V DocumentsRepository.DeleteDocumentAsync ====");
            Console.WriteLine(ex);
            throw;
        }
    }

    //  Úprava obsahu
    public async Task UpdateDocumentContentAsync(int documentId, IFormFile file, string updatedBy)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        try
        {
            await using var conn = await DatabaseManager.GetOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "dok_upravit";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            cmd.Parameters.Add("p_id", OracleDbType.Int32, documentId, ParameterDirection.Input);
            cmd.Parameters.Add("p_obsah", OracleDbType.Blob, bytes, ParameterDirection.Input);
            cmd.Parameters.Add("p_kdo", OracleDbType.Varchar2, updatedBy, ParameterDirection.Input);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("==== CHYBA V DocumentsRepository.UpdateDocumentContentAsync ====");
            Console.WriteLine(ex);
            throw;
        }
    }
}
