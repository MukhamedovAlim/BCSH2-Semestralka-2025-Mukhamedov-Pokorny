using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types; // kvůli OracleDecimal

namespace FitnessCenter.Infrastructure.Repositories
{
    // Řádek pro listování vybavení (co posíláme do Controlleru → ViewModelu)
    public sealed class EquipmentRow
    {
        public int Id { get; init; }
        public string Nazev { get; init; } = "";
        /// <summary>Uložená hodnota v DB: 'K' | 'P' | 'V'</summary>
        public string Typ { get; init; } = "";
        public string Stav { get; init; } = "OK";
        public string Fitko { get; init; } = "";
        public int FitkoId { get; init; }
    }

    // DTO pro create/edit formulář (Admin)
    public sealed class EquipmentEditDto
    {
        public int Id { get; set; }                 // 0 při Create
        public string Nazev { get; set; } = "";
        /// <summary>DB kód: 'K' | 'P' | 'V'</summary>
        public string Typ { get; set; } = "K";
        /// <summary>Stav: např. OK / Oprava / Mimo provoz</summary>
        public string Stav { get; set; } = "OK";
        public int FitkoId { get; set; }
    }

    public sealed class EquipmentHierarchyRow
    {
        public int Depth { get; set; }
        public string NodeType { get; set; } = default!;
        public string Label { get; set; } = default!;
        public int? FitnessId { get; set; }
        public int? EquipmentId { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DestroyedAt { get; set; }

        public DateTime? RepairedAt { get; set; }
    }

    public sealed class FitnessCenterRow
    {
        public int Id { get; set; }
        public string Nazev { get; set; } = "";
    }


    public sealed class EquipmentRepository
    {
        private static async Task<OracleConnection> OpenAsync()
            => (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

        // LIST s volitelnými filtry
        public async Task<IReadOnlyList<EquipmentRow>> GetAsync(string? typFilter, int? fitkoId)
        {
            static string? ToDbTyp(string? f) => f?.Trim().ToUpperInvariant() switch
            {
                "KARDIO" => "K",
                "POSILOVACÍ" or "POSILOVACI" => "P",
                "VOLNÁ ZÁVAŽÍ" or "VOLNA ZAVAZI" or "VOLNÁ ZÁVAZI" => "V",
                "K" or "P" or "V" => f.Trim().ToUpperInvariant(),
                _ => null
            };

            var typDb = ToDbTyp(typFilter);

            var items = new List<EquipmentRow>();
            using var con = await OpenAsync();

            var sql = @"
SELECT
  v.idvybaveni                 AS Id,
  v.nazev                      AS Nazev,
  v.typ                        AS Typ,           -- 'K'/'P'/'V'
  NVL(v.stav, 'OK')            AS Stav,
  fc.nazev                     AS Fitko,
  v.fitnesscentrum_idfitness   AS FitkoId
FROM vybaveni v
JOIN fitnesscentra fc ON fc.idfitness = v.fitnesscentrum_idfitness
WHERE 1=1";

            if (!string.IsNullOrEmpty(typDb)) sql += " AND v.typ = :typ";
            if (fitkoId is > 0) sql += " AND v.fitnesscentrum_idfitness = :f";
            sql += " ORDER BY fc.nazev, v.nazev";

            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            if (!string.IsNullOrEmpty(typDb))
                cmd.Parameters.Add("typ", OracleDbType.Char, 1).Value = typDb!;
            if (fitkoId is > 0)
                cmd.Parameters.Add("f", OracleDbType.Int32).Value = fitkoId!.Value;

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                items.Add(new EquipmentRow
                {
                    Id = rd.GetInt32(0),
                    Nazev = rd.GetString(1),
                    Typ = rd.GetString(2),
                    Stav = rd.IsDBNull(3) ? "OK" : rd.GetString(3),
                    Fitko = rd.GetString(4),
                    FitkoId = rd.GetInt32(5)
                });
            }

            return items;
        }

        /// <summary>Vytvoření vybavení (Admin). Vrací ID nového záznamu.</summary>
        public async Task<int> CreateAsync(EquipmentEditDto m, string kdo)
        {
            using var con = await OpenAsync();
            using var tx = con.BeginTransaction();

            try
            {
                const string sql = @"
INSERT INTO VYBAVENI (IDVYBAVENI, NAZEV, TYP, STAV, FITNESSCENTRUM_IDFITNESS)
VALUES (S_VYBAVENI.NEXTVAL, :n, :t, :s, :f)
RETURNING IDVYBAVENI INTO :id";

                int newId;

                using (var cmd = new OracleCommand(sql, con) { BindByName = true, Transaction = tx })
                {
                    cmd.Parameters.Add("n", OracleDbType.Varchar2).Value = (m.Nazev ?? "").Trim();
                    cmd.Parameters.Add("t", OracleDbType.Char, 1).Value = (m.Typ ?? "K").Trim().ToUpperInvariant();  // 'K'/'P'/'V'
                    cmd.Parameters.Add("s", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(m.Stav) ? "OK" : m.Stav.Trim();
                    cmd.Parameters.Add("f", OracleDbType.Int32).Value = m.FitkoId;

                    // OUT param – Oracle vrací OracleDecimal
                    var pId = new OracleParameter("id", OracleDbType.Int32)
                    { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(pId);

                    await cmd.ExecuteNonQueryAsync();

                    // Bezpečné čtení hodnoty
                    object raw = pId.Value;
                    if (raw is OracleDecimal od && !od.IsNull)
                        newId = (int)od.Value;
                    else if (raw is decimal dec)
                        newId = (int)dec;
                    else if (raw is int i)
                        newId = i;
                    else if (raw != null && int.TryParse(raw.ToString(), out var parsed))
                        newId = parsed;
                    else
                        throw new InvalidOperationException("Nepodařilo se přečíst ID nového vybavení.");
                }

                using (var log = new OracleCommand(
                    "INSERT INTO LOG_OPERACE(TABULKA,OPERACE,KDO,POPIS) VALUES('VYBAVENI','INSERT',:kdo,:popis)",
                    con)
                { BindByName = true, Transaction = tx })
                {
                    log.Parameters.Add("kdo", OracleDbType.Varchar2).Value = kdo;
                    log.Parameters.Add("popis", OracleDbType.Varchar2).Value = $"Přidáno: {m.Nazev} ({m.Typ}/{m.Stav})";
                    await log.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return newId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateAsync(EquipmentEditDto m, string kdo)
        {
            using var con = await OpenAsync();
            using var tx = con.BeginTransaction();

            try
            {
                int rows = 0;

                using (var cmd = new OracleCommand("SP_VYBAVENI_UPD", con)
                {
                    BindByName = true,
                    Transaction = tx,
                    CommandType = System.Data.CommandType.StoredProcedure
                })
                {
                    cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = m.Id;
                    cmd.Parameters.Add("p_nazev", OracleDbType.Varchar2).Value = (m.Nazev ?? "").Trim();
                    cmd.Parameters.Add("p_typ", OracleDbType.Char, 1).Value = (m.Typ ?? "K").Trim().ToUpperInvariant();
                    cmd.Parameters.Add("p_stav", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(m.Stav) ? "OK" : m.Stav.Trim();
                    cmd.Parameters.Add("p_fitko", OracleDbType.Int32).Value = m.FitkoId;

                    var pRows = new OracleParameter("p_rows", OracleDbType.Int32)
                    { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(pRows);

                    await cmd.ExecuteNonQueryAsync();
                    rows = Convert.ToInt32(pRows.Value.ToString());
                }

                using (var log = new OracleCommand(
                    "INSERT INTO LOG_OPERACE(TABULKA,OPERACE,KDO,POPIS) VALUES('VYBAVENI','UPDATE',:kdo,:popis)", con)
                { BindByName = true, Transaction = tx })
                {
                    log.Parameters.Add("kdo", OracleDbType.Varchar2).Value = kdo;
                    log.Parameters.Add("popis", OracleDbType.Varchar2).Value =
                        $"Upraveno ID={m.Id}: {m.Nazev} ({m.Typ}/{m.Stav})";
                    await log.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return rows > 0;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>Načtení pro edit formulář.</summary>
        public async Task<EquipmentEditDto?> GetByIdAsync(int id)
        {
            using var con = await OpenAsync();
            using var cmd = new OracleCommand(@"
SELECT v.idvybaveni, v.nazev, v.typ, NVL(v.stav,'OK'), v.fitnesscentrum_idfitness
  FROM vybaveni v
 WHERE v.idvybaveni = :id", con)
            { BindByName = true };

            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;

            return new EquipmentEditDto
            {
                Id = rd.GetInt32(0),
                Nazev = rd.GetString(1),
                Typ = rd.GetString(2),
                Stav = rd.GetString(3),
                FitkoId = rd.GetInt32(4)
            };
        }

        public async Task<bool> DeleteAsync(int id, string kdo)
        {
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var tx = con.BeginTransaction();

            try
            {
                int rows;

                using (var cmd = new OracleCommand(
                    "DELETE FROM VYBAVENI WHERE IDVYBAVENI = :id", con)
                { BindByName = true, Transaction = tx })
                {
                    cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;
                    rows = await cmd.ExecuteNonQueryAsync();
                }

                // Log
                using (var log = new OracleCommand(
                    "INSERT INTO LOG_OPERACE(TABULKA,OPERACE,KDO,POPIS) " +
                    "VALUES('VYBAVENI','DELETE',:kdo,:popis)", con)
                { BindByName = true, Transaction = tx })
                {
                    log.Parameters.Add("kdo", OracleDbType.Varchar2).Value = kdo;
                    log.Parameters.Add("popis", OracleDbType.Varchar2).Value = $"Smazáno ID={id}";
                    await log.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return rows > 0;
            }
            catch (OracleException ox) when (ox.Number == 2292)
            {
                tx.Rollback();
                throw new InvalidOperationException(
                    "Nelze smazat – existují na něj navázané záznamy.", ox);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<List<EquipmentHierarchyRow>> GetEquipmentHierarchyAsync(int? fitnessId)
        {
            const string sql = @"
        SELECT LEVEL       AS depth,
               node_type,
               label,
               idfitness,
               idvybaveni,
               datum_vytvoreni,
               datum_upravy,
               datum_zniceni,
               datum_opravy
        FROM V_FITKO_VYBAVENI_HIER
        START WITH parent_id IS NULL
           AND (:p_fitko IS NULL OR idfitness = :p_fitko)
        CONNECT BY PRIOR node_id = parent_id
        ORDER SIBLINGS BY label";

            var result = new List<EquipmentHierarchyRow>();

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };

            cmd.Parameters.Add("p_fitko", OracleDbType.Int32)
                .Value = fitnessId.HasValue ? (object)fitnessId.Value : DBNull.Value;

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new EquipmentHierarchyRow
                {
                    Depth = reader.GetInt32(0),
                    NodeType = reader.GetString(1),
                    Label = reader.GetString(2),
                    FitnessId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                    EquipmentId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    CreatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    DestroyedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    RepairedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }


            return result;
        }

        public async Task<List<FitnessCenterRow>> GetFitnessCentersAsync()
        {
            const string sql = @"
        SELECT idfitness, nazev
        FROM fitnesscentra
        ORDER BY nazev";

            var list = new List<FitnessCenterRow>();

            using var con = await OpenAsync();
            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            using var rd = await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                list.Add(new FitnessCenterRow
                {
                    Id = rd.GetInt32(0),
                    Nazev = rd.GetString(1)
                });
            }

            return list;
        }
    }
}
