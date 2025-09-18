using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbLab.Core.Models
{
    public sealed class QueryResult
    {
        // Row sets serialized as simple dictionaries for now
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
            = Array.Empty<IReadOnlyDictionary<string, object?>>();

        public string? Message { get; init; }           // e.g., "(42 rows affected)"
        public TimeSpan Duration { get; init; } = TimeSpan.Zero;
        public bool Succeeded { get; init; } = true;
        public string? Error { get; init; }             // populated when Succeeded = false
    }
}
