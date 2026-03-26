using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SQLTestDataScriptGenerator
{
    internal static class SchemaBaselineStore
    {
        private const string BaselineFile = "_SchemaBaseline.json";

        private sealed class BaselineContainer
        {
            public List<SchemaBaseline> Databases { get; set; } = new();
        }

        private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        private static BaselineContainer LoadContainer()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BaselineFile);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<BaselineContainer>(json, _opts);
                    if (data != null) return data;
                }
            }
            catch { }
            return new BaselineContainer();
        }

        private static void SaveContainer(BaselineContainer container)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BaselineFile);
                var json = JsonSerializer.Serialize(container, _opts);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public static SchemaBaseline GetOrCreate(string dbKey)
        {
            var container = LoadContainer();
            var existing = container.Databases.FirstOrDefault(d => string.Equals(d.DatabaseKey, dbKey, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;
            existing = new SchemaBaseline { DatabaseKey = dbKey };
            container.Databases.Add(existing);
            SaveContainer(container);
            return existing;
        }

        public static void Update(SchemaBaseline baseline)
        {
            var container = LoadContainer();
            var idx = container.Databases.FindIndex(d => string.Equals(d.DatabaseKey, baseline.DatabaseKey, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) container.Databases[idx] = baseline; else container.Databases.Add(baseline);
            SaveContainer(container);
        }

        public static bool Exists(string dbKey)
        {
            var container = LoadContainer();
            return container.Databases.Any(d => string.Equals(d.DatabaseKey, dbKey, StringComparison.OrdinalIgnoreCase));
        }

        public static SchemaBaseline? TryGet(string dbKey)
        {
            var container = LoadContainer();
            return container.Databases.FirstOrDefault(d => string.Equals(d.DatabaseKey, dbKey, StringComparison.OrdinalIgnoreCase));
        }
    }
}
