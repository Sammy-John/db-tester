using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbLab.Core.Models
{
    public sealed class TableDetail
    {
        public string Schema { get; init; } = "dbo";
        public string Name { get; init; } = "";

        // Simple placeholders for now
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> PrimaryKey { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> UniqueIndexes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ForeignKeys { get; init; } = Array.Empty<string>();
    }
}

