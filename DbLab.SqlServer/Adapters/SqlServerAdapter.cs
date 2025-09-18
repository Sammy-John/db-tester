using System.Data;
using System.Diagnostics;
using DbLab.Core.Interfaces;
using DbLab.Core.Models;
using Microsoft.Data.SqlClient;
using Dapper;

namespace DbLab.SqlServer.Adapters
{
    public sealed class SqlServerAdapter : IDbAdapter
    {
        private readonly string _connectionString;

        public SqlServerAdapter(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        /// <summary>
        /// Execute arbitrary SQL and return rows + duration + messages/errors.
        /// - Uses Dapper to read result set into dictionaries (column -> value).
        /// - If the SQL doesn't return rows (e.g., DDL), we still return a message and duration.
        /// </summary>
        public async Task<QueryResult> RunQueryAsync(string sql, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return new QueryResult { Succeeded = false, Error = "SQL is empty." };

            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync(ct);

                var sw = Stopwatch.StartNew();

                // Try read result set if any. If none, ExecuteAsync still gives us rows affected.
                // We’ll attempt a buffered read of the first result set; if no reader is returned,
                // fall back to ExecuteAsync for rows affected message.
                try
                {
                    using var reader = await conn.ExecuteReaderAsync(
                        new CommandDefinition(sql, cancellationToken: ct, commandType: CommandType.Text));

                    var rows = new List<IReadOnlyDictionary<string, object?>>();

                    if (reader.FieldCount > 0)
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var name = reader.GetName(i);
                                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                dict[name] = val;
                            }
                            rows.Add(dict);
                        }
                    }

                    sw.Stop();
                    var msg = rows.Count > 0
                        ? $"Returned {rows.Count} row(s)."
                        : "Command completed (no rows).";

                    return new QueryResult
                    {
                        Rows = rows,
                        Message = msg,
                        Duration = sw.Elapsed,
                        Succeeded = true
                    };
                }
                catch
                {
                    // If ExecuteReaderAsync failed because there’s no resultset (e.g., DDL),
                    // fall back to ExecuteAsync to report rows affected.
                    var affected = await conn.ExecuteAsync(
                        new CommandDefinition(sql, cancellationToken: ct, commandType: CommandType.Text));

                    sw.Stop();

                    return new QueryResult
                    {
                        Rows = Array.Empty<IReadOnlyDictionary<string, object?>>(),
                        Message = $"({affected} row(s) affected)",
                        Duration = sw.Elapsed,
                        Succeeded = true
                    };
                }
            }
            catch (Exception ex)
            {
                return new QueryResult
                {
                    Succeeded = false,
                    Error = ex.Message,
                    Message = "Error executing query."
                };
            }
        }

        public async Task<IReadOnlyList<DbTable>> ListTablesAsync(string schema = "dbo")
        {
            // Return empty if schema not provided
            if (string.IsNullOrWhiteSpace(schema)) schema = "dbo";

            // We’ll:
            // 1) read tables from INFORMATION_SCHEMA.TABLES
            // 2) left join approximate rowcount from sys.dm_db_partition_stats
            //    (clustered heap/clustered index rows only; ignores internal objects)
            const string sql = @"
                WITH RowCounts AS (
                    SELECT
                        OBJECT_SCHEMA_NAME(p.[object_id]) AS [SchemaName],
                        OBJECT_NAME(p.[object_id])        AS [TableName],
                        SUM(p.[row_count])                AS [ApproxRowCount]
                    FROM sys.dm_db_partition_stats AS p
                    WHERE p.[index_id] IN (0, 1)     -- heap or clustered index
                      AND p.[object_id] > 0          -- user objects
                    GROUP BY OBJECT_SCHEMA_NAME(p.[object_id]), OBJECT_NAME(p.[object_id])
                )
                SELECT
                    t.TABLE_SCHEMA   AS [Schema],
                    t.TABLE_NAME     AS [Name],
                    CAST(rc.ApproxRowCount AS BIGINT) AS [ApproxRowCount]
                FROM INFORMATION_SCHEMA.TABLES AS t
                LEFT JOIN RowCounts rc
                    ON rc.SchemaName = t.TABLE_SCHEMA
                   AND rc.TableName  = t.TABLE_NAME
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                  AND t.TABLE_SCHEMA = @schema
                ORDER BY t.TABLE_NAME;
                ";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<(string Schema, string Name, long? ApproxRowCount)>(
                sql, new { schema });

            // Map to Core model
            var result = rows.Select(r => new DbTable
            {
                Schema = r.Schema,
                Name = r.Name,
                ApproxRowCount = r.ApproxRowCount
            }).ToList();

            return result;
        }


        public async Task<TableDetail> GetTableAsync(string schema, string table)
        {
            if (string.IsNullOrWhiteSpace(schema)) throw new ArgumentException("schema required", nameof(schema));
            if (string.IsNullOrWhiteSpace(table)) throw new ArgumentException("table required", nameof(table));

            const string columnsSql = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION;";

                        const string pkSql = @"
            SELECT kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
             AND kcu.CONSTRAINT_NAME  = tc.CONSTRAINT_NAME
            WHERE tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table
              AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ORDER BY kcu.ORDINAL_POSITION;";

                        const string uniqueSql = @"
            SELECT kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
             AND kcu.CONSTRAINT_NAME  = tc.CONSTRAINT_NAME
            WHERE tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table
              AND tc.CONSTRAINT_TYPE = 'UNIQUE'
            ORDER BY kcu.ORDINAL_POSITION;";

                        const string fkSql = @"
            SELECT
                fk = fk.name,
                referencing_column = c1.name,
                referenced_table   = OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id),
                referenced_column  = c2.name
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc
              ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c1
              ON c1.object_id = fkc.parent_object_id AND c1.column_id = fkc.parent_column_id
            JOIN sys.columns c2
              ON c2.object_id = fkc.referenced_object_id AND c2.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = OBJECT_ID(@schema + '.' + @table)
            ORDER BY fk.name, fkc.constraint_column_id;";

            using var conn = CreateConnection();
            await conn.OpenAsync();

            // columns
            var cols = await conn.QueryAsync(columnsSql, new { schema, table });
            var columnNames = cols.Select(r =>
            {
                // compact friendly description like: Name NVARCHAR(100) NOT NULL
                string name = (string)r.COLUMN_NAME;
                string type = (string)r.DATA_TYPE;
                string nullable = ((string)r.IS_NULLABLE) == "YES" ? "NULL" : "NOT NULL";
                var maxLen = (int?)r.CHARACTER_MAXIMUM_LENGTH;
                var prec = (byte?)r.NUMERIC_PRECISION;
                var scale = (int?)r.NUMERIC_SCALE;

                string typeDisplay = type.ToUpperInvariant();
                if (maxLen is not null && maxLen > 0)
                    typeDisplay += $"({(maxLen == -1 ? "MAX" : maxLen)})";
                else if (prec is not null && scale is not null && prec > 0)
                    typeDisplay += $"({prec},{scale})";

                return $"{name} {typeDisplay} {nullable}";
            }).ToList();

            // primary key columns
            var pkCols = (await conn.QueryAsync<string>(pkSql, new { schema, table })).ToList();

            // unique index/constraint columns (flattened list for now)
            var uniqueCols = (await conn.QueryAsync<string>(uniqueSql, new { schema, table })).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // foreign keys (flatten to readable strings)
            var fks = await conn.QueryAsync(fkSql, new { schema, table });
            var fkLines = fks.Select(r =>
                $"{(string)r.fk}: {(string)r.referencing_column} → {(string)r.referenced_table}({(string)r.referenced_column})"
            ).ToList();

            return new TableDetail
            {
                Schema = schema,
                Name = table,
                Columns = columnNames,
                PrimaryKey = pkCols,
                UniqueIndexes = uniqueCols,
                ForeignKeys = fkLines
            };
        }


        public Task SeedAsync(SeedPack pack, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task ResetAsync(SeedPack pack, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
