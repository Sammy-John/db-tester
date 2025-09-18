using System.Linq;
using System.Threading.Tasks;
using DbLab.SqlServer.Adapters;
using Xunit;
using Xunit.Abstractions;

namespace DbLab.Tests
{
    public class SqlServerAdapter_DataPeek
    {
        private readonly ITestOutputHelper _output;
        public SqlServerAdapter_DataPeek(ITestOutputHelper output) => _output = output;

        private const string ConnStr =
            "Server=.;Database=DbLab_Retail;Trusted_Connection=True;Encrypt=False";

        [Fact]
        public async Task Dump_Top5_From_First_Table_If_Any()
        {
            var sut = new SqlServerAdapter(ConnStr);

            var tables = await sut.ListTablesAsync("dbo");
            var first = tables.FirstOrDefault();
            Assert.NotNull(tables); // proves call worked

            if (first is null)
            {
                _output.WriteLine("No tables found in dbo.");
                return; // nothing to dump
            }

            var fullName = $"[{first.Schema}].[{first.Name}]";
            var result = await sut.RunQueryAsync($"SELECT TOP 5 * FROM {fullName};");
            Assert.True(result.Succeeded);

            if (result.Rows.Count == 0)
            {
                _output.WriteLine($"Table {fullName} exists but returned 0 rows.");
                return;
            }

            // Pretty-print rows
            for (int i = 0; i < result.Rows.Count; i++)
            {
                var row = result.Rows[i];
                var line = string.Join(" | ", row.Select(kv => $"{kv.Key}={kv.Value ?? "NULL"}"));
                _output.WriteLine($"Row {i + 1}: {line}");
            }
        }
    }
}
