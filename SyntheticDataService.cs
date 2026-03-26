using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SQLTestDataScriptGenerator
{
    internal sealed class SyntheticDataService
    {
        // Loads synthetic rows for empty tables by searching snapshot JSON files for matching canonical DB
        // Returns map of (Schema,Table) => list of row dictionaries with already literal-formatted values
        public Dictionary<(string Schema,string Name), List<Dictionary<string,string>>> LoadSyntheticRowsForEmptyTables(string baseDirectory, string currentCanonicalDb, HashSet<(string Schema,string Name)> emptyTables, SqlConnection liveConn)
        {
            var result = new Dictionary<(string Schema,string Name), List<Dictionary<string,string>>>(emptyTables.Count);
            if (emptyTables.Count == 0) return result;
            try
            {
                // Gather all snapshot files that correspond to this canonical name (including version variants previously captured)
                var snapshotFiles = Directory.GetFiles(baseDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .Where(f => string.Equals(Path.GetFileNameWithoutExtension(f), currentCanonicalDb, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (snapshotFiles.Count == 0) return result;
                // Build lookup of required table keys
                var needed = emptyTables.Select(t => t.Schema + "." + t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var file in snapshotFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var snapshot = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, object?>>>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (snapshot == null) continue;
                        foreach (var kv in snapshot)
                        {
                            if (!needed.Contains(kv.Key)) continue;
                            var parts = kv.Key.Split('.'); if (parts.Length != 2) continue;
                            var schema = parts[0]; var table = parts[1];
                            // Verify insertable columns exist in live database; remove columns that are non-insertable (identity/computed/timestamp/rowversion/system-like)
                            var liveCols = GetInsertableLiveColumns(liveConn, schema, table);
                            if (liveCols.Count == 0) continue;
                            var preparedRows = new List<Dictionary<string,string>>();
                            foreach (var row in kv.Value)
                            {
                                var dict = new Dictionary<string,string>();
                                foreach (var col in liveCols)
                                {
                                    if (!row.TryGetValue(col, out var raw)) raw = null;
                                    dict[col] = ToSqlLiteral(raw);
                                }
                                preparedRows.Add(dict);
                            }
                            if (preparedRows.Count > 0 && !result.ContainsKey((schema, table)))
                                result[(schema, table)] = preparedRows;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        // Returns insertable column names excluding identity/computed/timestamp/rowversion and system-like names
        private HashSet<string> GetInsertableLiveColumns(SqlConnection conn, string schema, string table)
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = new SqlCommand(@"SELECT c.name, ty.name, c.is_identity, c.is_computed
FROM sys.columns c
JOIN sys.tables t ON c.object_id=t.object_id
JOIN sys.schemas s ON t.schema_id=s.schema_id
JOIN sys.types ty ON c.user_type_id=ty.user_type_id AND c.system_type_id=ty.system_type_id
WHERE s.name=@s AND t.name=@t
ORDER BY c.column_id", conn);
                cmd.Parameters.AddWithValue("@s", schema); cmd.Parameters.AddWithValue("@t", table);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var name = rdr.GetString(0);
                    var type = rdr.GetString(1);
                    bool isIdentity = rdr.GetBoolean(2);
                    bool isComputed = rdr.GetBoolean(3);
                    if (isIdentity || isComputed) continue;
                    if (type.Equals("timestamp", StringComparison.OrdinalIgnoreCase) || type.Equals("rowversion", StringComparison.OrdinalIgnoreCase)) continue;
                    var lower = name.ToLowerInvariant();
                    if (lower == "iid" || lower == "rversion" || lower == "timestamp") continue;
                    cols.Add(name);
                }
            }
            catch { }
            return cols;
        }

        private static string ToSqlLiteral(object? value)
        {
            if (value == null) return "NULL";
            return value switch
            {
                string s => "'" + s.Replace("'", "''") + "'",
                DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff") + "'",
                bool b => b ? "1" : "0",
                byte[] bytes => "0x" + BitConverter.ToString(bytes).Replace("-", string.Empty),
                Guid g => "'" + g.ToString() + "'",
                float or double or decimal => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL",
                _ => "'" + value.ToString()?.Replace("'", "''") + "'"
            };
        }
    }
}
