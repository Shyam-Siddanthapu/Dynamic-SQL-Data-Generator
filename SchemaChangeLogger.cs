using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SQLTestDataScriptGenerator
{
    internal static class SchemaChangeLogger
    {
        private const string LogFile = "_SchemaChanges.log";
        private static readonly object _lock = new();

        private static string GetPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFile);

        private static void Append(string text)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(GetPath(), text, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void LogInfo(string message)
        {
            Append($"[{DateTime.UtcNow:O}] INFO  {message}\n");
        }

        public static void LogWarning(string message)
        {
            Append($"[{DateTime.UtcNow:O}] WARN  {message}\n");
        }

        public static void LogChange(string dbKey, string category, IEnumerable<string> items)
        {
            var list = items?.ToList() ?? new List<string>();
            if (list.Count == 0) return;
            var header = $"[{DateTime.UtcNow:O}] CHANGE Database={dbKey} Category={category} Count={list.Count}";
            Append(header + "\n" + string.Join("\n", list.Select(i => "  - " + i)) + "\n");
        }
    }
}
