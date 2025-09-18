using DbLab.Core.Models;

namespace DbLab.Core.Interfaces
{
    public interface IDbAdapter
    {
        Task<IReadOnlyList<DbTable>> ListTablesAsync(string schema = "dbo");
        Task<TableDetail> GetTableAsync(string schema, string table);
        Task<QueryResult> RunQueryAsync(string sql, CancellationToken ct = default);
        Task SeedAsync(SeedPack pack, CancellationToken ct = default);
        Task ResetAsync(SeedPack pack, CancellationToken ct = default);
    }
}
