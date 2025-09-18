using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DbLab.Core.Models;
using DbLab.SqlServer.Adapters;

namespace DbLab.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SqlServerAdapter _adapter;

        [ObservableProperty]
        private string _connectionString =
            "Server=.;Database=DbLab_Retail;Trusted_Connection=True;Encrypt=False";

        [ObservableProperty] private string _queryText = "SELECT 1 AS One;";
        [ObservableProperty] private DataView? _results;
        [ObservableProperty] private string? _status;

        public ObservableCollection<DbTable> Tables { get; } = new();

        public MainViewModel()
        {
            _adapter = new SqlServerAdapter(_connectionString);
        }

        [RelayCommand]
        private async Task RefreshTablesAsync()
        {
            Tables.Clear();
            var list = await _adapter.ListTablesAsync("dbo");
            foreach (var t in list) Tables.Add(t);
            Status = $"Loaded {Tables.Count} table(s).";
        }

        [RelayCommand]
        private async Task RunQueryAsync()
        {
            var result = await _adapter.RunQueryAsync(QueryText);
            Status = result.Succeeded
                ? $"{result.Message} in {result.Duration.TotalMilliseconds:N0} ms"
                : $"ERROR: {result.Error}";

            Results = result.Rows.Count == 0
                ? null
                : ToDataTable(result).DefaultView;
        }

        private static DataTable ToDataTable(QueryResult result)
        {
            var dt = new DataTable();
            var first = result.Rows.First();
            foreach (var col in first.Keys) dt.Columns.Add(col);

            foreach (var row in result.Rows)
            {
                var values = first.Keys.Select(k => row.TryGetValue(k, out var v) ? v : null).ToArray();
                dt.Rows.Add(values);
            }
            return dt;
        }
    }
}
