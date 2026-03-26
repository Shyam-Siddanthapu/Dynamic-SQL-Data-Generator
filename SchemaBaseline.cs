using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SQLTestDataScriptGenerator
{
    // Represents persisted schema snapshot for a canonical database
    public sealed class SchemaBaseline
    {
        public string DatabaseKey { get; set; } = string.Empty; // canonical db name
        // Table name => columns hash set
        public Dictionary<string, HashSet<string>> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
