using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbLab.Core.Models
{
    public sealed class DbTable
    {
        public string Schema { get; init; } = "dbo";
        public string Name { get; init; } = "";
        public long? ApproxRowCount { get; init; }
    }
}

