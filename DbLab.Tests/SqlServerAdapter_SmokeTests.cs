using System.Linq;
using System.Threading.Tasks;
using DbLab.SqlServer.Adapters;
using Xunit;

public class SqlServerAdapter_GenericSmokeTests
{
    // 👉 Pick ONE of these connection strings:
    // Local default instance (Windows Auth):
    private const string ConnStr = "Server=.;Database=DbLab_Retail;Trusted_Connection=True;Encrypt=False";

    // SQL Express example:
    // private const string ConnStr = "Server=.\\SQLEXPRESS;Database=DbLab_Retail;Trusted_Connection=True;Encrypt=False";

    // SQL Auth example:
    // private const string ConnStr = "Server=.;Database=DbLab_Retail;User ID=sa;Password=YourStrong!Passw0rd;Encrypt=False";

    [Fact]
    public async Task Can_Run_Trivial_Query_Without_Tables()
    {
        var sut = new SqlServerAdapter(ConnStr);
        var result = await sut.RunQueryAsync("SELECT 1 AS One;");
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Rows);
        Assert.Equal(1, (int)(result.Rows.First()["One"]!));
    }

    [Fact]
    public async Task Can_List_Tables_Generically_And_Optionally_Select_Top_1()
    {
        var sut = new SqlServerAdapter(ConnStr);

        // Prove call works regardless of schema contents:
        var tables = await sut.ListTablesAsync("dbo");
        Assert.NotNull(tables); // no exception, valid result

        // If there ARE tables, try a dynamic SELECT TOP (1) safely
        var first = tables.FirstOrDefault();
        if (first is not null)
        {
            // Build a safe, quoted identifier: [schema].[table]
            var fullName = $"[{first.Schema}].[{first.Name}]";
            var select = $"SELECT TOP 1 * FROM {fullName};";

            var result = await sut.RunQueryAsync(select);
            Assert.True(result.Succeeded);
            // We don't assert rows because table may be empty — just ensure no error
        }
    }
}
