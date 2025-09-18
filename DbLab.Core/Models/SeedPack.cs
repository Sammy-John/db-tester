using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbLab.Core.Models
{
    public sealed class SeedPack
    {
        // Path hints or embedded resource names; we’ll flesh out later
        public string Name { get; init; } = "Retail";
        public string? CreateScriptPath { get; init; }
        public string? ConstraintsScriptPath { get; init; }
        public string? ResetScriptPath { get; init; }
        public IReadOnlyDictionary<string, string>? CsvPaths { get; init; } // table -> csv path
    }
}
