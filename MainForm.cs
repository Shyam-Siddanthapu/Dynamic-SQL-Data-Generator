using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace SQLTestDataScriptGenerator
{
    public partial class MainForm : Form
    {
        // === Fields ===
        private List<string> _allDatabases = new();
        private bool _loadingDatabases;
        private string? _currentDatabase;
        private readonly Dictionary<(string Schema, string Name), List<Dictionary<string, string>>> _pendingSampleRows = new();
        private List<TableEntry> _allTablesForCurrentDb = new();
        private List<(string Parent, string Child, string Column)> _inferredColumnDeps = new();
        private const string GlobalRelationshipsFile = "_GlobalRelationships.json"; // single file only
        private List<GlobalRelationship> _globalRelationships = new();
        private List<GlobalRelationship> _allRelationships = new();
        private Dictionary<string, List<GlobalRelationship>> _childToParents = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<GlobalRelationship>> _parentToChildren = new(StringComparer.OrdinalIgnoreCase);
        private string _currentCanonicalDb = string.Empty;
        private bool _relationshipDeclined;
        private readonly Dictionary<string, List<string>> _columnReferenceSamples = new(StringComparer.OrdinalIgnoreCase); // collected column sample values
        // Add field for current baseline reference
        private SchemaBaseline? _currentBaseline; // schema snapshot
        // Snapshot constants
        private const int SnapshotRowLimit = 20;
        private const string SnapshotFileExtension = ".json"; // canonical db name + .json
        private const string ExistedDataFolder = "_ExistedData"; // new folder for persisted data snapshots
                                                                                                                           // === Models ===
        private sealed class GlobalRelationship
        {
            public string DatabaseKey { get; set; } = string.Empty;
            public string ParentSchema { get; set; } = string.Empty;
            public string ParentTable { get; set; } = string.Empty;
            public string ParentColumn { get; set; } = string.Empty;
            public string ChildSchema { get; set; } = string.Empty;
            public string ChildTable { get; set; } = string.Empty;
            public string ChildColumn { get; set; } = string.Empty;
            public string RelationshipType { get; set; } = "Referential";
            public string ParentFull => ParentSchema + "." + ParentTable;
            public string ChildFull => ChildSchema + "." + ChildTable;
        }
        private sealed class TableEntry
        {
            public string Schema { get; }
            public string Name { get; }
            public long RowCount { get; }
            public string FullName => Schema + "." + Name;
            public TableEntry(string schema, string name, long rowCount) { Schema = schema; Name = name; RowCount = rowCount; }
            public override string ToString() => RowCount >= 0 ? $"{Schema}.{Name} ({RowCount:N0})" : $"{Schema}.{Name} (..)";
        }
        private sealed class ForeignKeyEdge
        {
            public string Parent { get; }
            public string Child { get; }
            public string? ParentColumn { get; }
            public string? ChildColumn { get; }
            public bool IsManual { get; }
            public ForeignKeyEdge(string parent, string child, string? parentCol, string? childCol, bool manual) { Parent = parent; Child = child; ParentColumn = parentCol; ChildColumn = childCol; IsManual = manual; }
        }
        private sealed class ExportResult
        {
            public string Table { get; init; } = string.Empty; // schema.table
            public int SampleRows { get; init; }
            public int DataRows { get; init; }
            public string? Error { get; init; }
            public int Total => SampleRows + DataRows;
        }
        // === Constructor ===
        public MainForm()
        {
            InitializeComponent();
            serverTextBox.Text = "AZRSSQLV002.CCP.LOCAL"; // default server restored
            statusLabel.Text = "Enter a server name and click Connect.";
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            if (manageRelationshipsButton != null) manageRelationshipsButton.Click += manageRelationshipsButton_Click;
            if (fkGraphButton != null) fkGraphButton.Click += fkGraphButton_Click;
            // Ensure buttons autosize to their text to reduce width
            if (autoMapButton != null) { autoMapButton.AutoSize = true; autoMapButton.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
            if (manageRelationshipsButton != null) { manageRelationshipsButton.AutoSize = true; manageRelationshipsButton.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
            if (fkGraphButton != null) { fkGraphButton.AutoSize = true; fkGraphButton.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
            if (selectAllTablesButton != null) { selectAllTablesButton.AutoSize = true; selectAllTablesButton.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
            if (clearTablesButton != null) { clearTablesButton.AutoSize = true; clearTablesButton.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
            this.Resize += (_, __) => LayoutTopControls();
            LayoutTopControls(); // initial layout fix for overlapping controls
        }
        // === Persistence ===
        private void LoadGlobalRelationships()
        {
            _globalRelationships.Clear(); _allRelationships.Clear(); _childToParents.Clear(); _parentToChildren.Clear();
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GlobalRelationshipsFile);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<GlobalRelationship>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (list != null)
                    {
                        foreach (var r in list)
                        {
                            if (string.IsNullOrWhiteSpace(r.RelationshipType)) r.RelationshipType = "Referential";
                            if (string.IsNullOrWhiteSpace(r.DatabaseKey)) r.DatabaseKey = "GLOBAL";
                        }
                        _allRelationships = list;
                    }
                }
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(_currentCanonicalDb))
                _globalRelationships = _allRelationships.Where(r => string.Equals(r.DatabaseKey, _currentCanonicalDb, StringComparison.OrdinalIgnoreCase) || r.DatabaseKey == "GLOBAL").ToList();
            IndexGlobalRelationships();
        }
        private void SaveGlobalRelationships()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GlobalRelationshipsFile);
                List<GlobalRelationship> existing = new();
                if (File.Exists(path))
                {
                    try
                    {
                        var jsonOld = File.ReadAllText(path);
                        var oldList = System.Text.Json.JsonSerializer.Deserialize<List<GlobalRelationship>>(jsonOld, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (oldList != null) existing = oldList;
                    }
                    catch { }
                }
                if (!string.IsNullOrWhiteSpace(_currentCanonicalDb))
                    existing = existing.Where(r => !string.Equals(r.DatabaseKey, _currentCanonicalDb, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var r in _globalRelationships)
                {
                    if (string.IsNullOrWhiteSpace(r.DatabaseKey)) r.DatabaseKey = _currentCanonicalDb;
                    existing.Add(r);
                }
                var json = System.Text.Json.JsonSerializer.Serialize(existing, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json); _allRelationships = existing;
            }
            catch { }
        }
        private void IndexGlobalRelationships()
        {
            _childToParents.Clear(); _parentToChildren.Clear();
            foreach (var r in _globalRelationships)
            {
                if (!_childToParents.TryGetValue(r.ChildFull, out var pl)) { pl = new List<GlobalRelationship>(); _childToParents[r.ChildFull] = pl; }
                pl.Add(r);
                if (!_parentToChildren.TryGetValue(r.ParentFull, out var cl)) { cl = new List<GlobalRelationship>(); _parentToChildren[r.ParentFull] = cl; }
                cl.Add(r);
            }
        }
        // === DB Access ===
        private SqlConnection OpenCurrentDb()
        {
            var csb = new SqlConnectionStringBuilder { DataSource = serverTextBox.Text.Trim(), InitialCatalog = _currentDatabase, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true };
            var c = new SqlConnection(csb.ConnectionString); c.Open(); return c;
        }
        private IEnumerable<string> GetColumnsForTable(SqlConnection conn, string schema, string table)
        {
            var cols = new List<string>();
            using var cmd = new SqlCommand(@"SELECT c.name FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name=@s AND t.name=@t ORDER BY c.column_id", conn);
            cmd.Parameters.AddWithValue("@s", schema); cmd.Parameters.AddWithValue("@t", table);
            using var rdr = cmd.ExecuteReader(); while (rdr.Read()) cols.Add(rdr.GetString(0)); return cols;
        }
        private static string GetCanonicalDbName(string db)
        {
            if (string.IsNullOrWhiteSpace(db)) return string.Empty;
            int us = db.IndexOf('_');
            if (us > 0)
            {
                var prefix = db.Substring(0, us);
                var segs = prefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
                bool versionLike = segs.Length >= 3 && segs.All(s => int.TryParse(s, out _));
                if (versionLike) return db.Substring(us + 1);
            }
            return db;
        }
        // === Inference ===
        private List<TableEntry> OrderByInferredDependencies(List<TableEntry> selected)
        {
            if (selected.Count == 0) return selected;
            var keys = selected.Select(t => t.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var edges = new List<(string Parent, string Child)>();
            foreach (var dep in _inferredColumnDeps) if (keys.Contains(dep.Parent) && keys.Contains(dep.Child)) edges.Add((dep.Parent, dep.Child));
            foreach (var gr in _globalRelationships) if (keys.Contains(gr.ParentFull) && keys.Contains(gr.ChildFull)) edges.Add((gr.ParentFull, gr.ChildFull));
            if (edges.Count == 0) return selected;
            var indegree = selected.ToDictionary(t => t.FullName, _ => 0, StringComparer.OrdinalIgnoreCase);
            var children = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (p, c) in edges) { if (!children.TryGetValue(p, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); children[p] = set; } if (set.Add(c)) indegree[c]++; }
            var q = new Queue<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key)); var ordered = new List<string>();
            while (q.Count > 0) { var k = q.Dequeue(); ordered.Add(k); if (children.TryGetValue(k, out var chs)) { foreach (var ch in chs) { indegree[ch]--; if (indegree[ch] == 0) q.Enqueue(ch); } } }
            foreach (var rem in indegree.Where(kv => kv.Value > 0).Select(kv => kv.Key)) if (!ordered.Contains(rem)) ordered.Add(rem);
            var mapSel = selected.ToDictionary(t => t.FullName, t => t, StringComparer.OrdinalIgnoreCase);
            return ordered.Select(o => mapSel[o]).ToList();
        }
        private async Task InferColumnDependenciesAsync(string server, string database)
        {
            _inferredColumnDeps.Clear();
            try
            {
                var csb = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true };
                using var conn = new SqlConnection(csb.ConnectionString); await conn.OpenAsync();
                const string sql = @"SELECT sch.name, t.name, c.name, ty.name, c.is_identity, c.is_computed
 FROM sys.tables t JOIN sys.schemas sch ON t.schema_id=sch.schema_id
 JOIN sys.columns c ON c.object_id=t.object_id
 JOIN sys.types ty ON c.user_type_id=ty.user_type_id AND c.system_type_id=ty.system_type_id
 ORDER BY sch.name,t.name,c.column_id";
                var firstOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                using var cmd = new SqlCommand(sql, conn); using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var schema = rdr.GetString(0); var table = rdr.GetString(1); var column = rdr.GetString(2); var type = rdr.GetString(3);
                    bool isId = rdr.GetBoolean(4); bool isComp = rdr.GetBoolean(5);
                    if (isId || isComp) continue; if (IsIgnoredInferenceColumn(column)) continue; if (IsSystemLikeColumnName(column)) continue; if (IsNonInsertableType(type)) continue;
                    var tableKey = schema + "." + table;
                    occurrences[column] = occurrences.TryGetValue(column, out var c) ? c + 1 : 1;
                    if (!firstOwner.TryGetValue(column, out var owner)) firstOwner[column] = tableKey; else if (!string.Equals(owner, tableKey, StringComparison.OrdinalIgnoreCase)) _inferredColumnDeps.Add((owner, tableKey, column));
                }
                if (_inferredColumnDeps.Count > 0) _inferredColumnDeps = _inferredColumnDeps.Where(d => occurrences.TryGetValue(d.Column, out var c) && c > 1).ToList();
            }
            catch { }
        }
        // === Loading indicators ===
        private void UpdateStatus(string message)
        { if (InvokeRequired) BeginInvoke(new Action(() => statusLabel.Text = message)); else statusLabel.Text = message; }
        private void BeginLoading(string message)
        { UpdateStatus(message); progressBar.Style = ProgressBarStyle.Marquee; progressBar.MarqueeAnimationSpeed = 30; UseWaitCursor = true; }
        private void EndLoading(string message)
        { UpdateStatus(message); progressBar.Style = ProgressBarStyle.Continuous; progressBar.MarqueeAnimationSpeed = 0; UseWaitCursor = false; }
        private void RefreshDatabasesInlineLabel() => label2.Text = $"Databases ({_allDatabases.Count})";
        private void UpdateDatabaseCount() => RefreshDatabasesInlineLabel();
        // === DB list events ===
        private void databaseSearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_allDatabases.Count == 0) return;
            var filter = databaseSearchTextBox.Text.Trim();
            var filtered = string.IsNullOrEmpty(filter) ? _allDatabases : _allDatabases.Where(d => d.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            _loadingDatabases = true; try { databasesListBox.DataSource = null; databasesListBox.DataSource = filtered; } finally { _loadingDatabases = false; }
            statusLabel.Text = string.IsNullOrEmpty(filter) ? $"Found {_allDatabases.Count} databases." : $"Filtered: {filtered.Count}/{_allDatabases.Count}";
        }
        private async void connectButton_Click(object sender, EventArgs e)
        {
            databasesListBox.DataSource = null; _allDatabases.Clear(); UpdateDatabaseCount(); _currentDatabase = null; tablesCheckedListBox.Items.Clear(); selectedDatabaseLabel.Text = "Selected Database: (none)"; exportButton.Enabled = false; BeginLoading("Starting connection..."); connectButton.Enabled = false;
            try
            {
                var server = serverTextBox.Text.Trim(); if (string.IsNullOrWhiteSpace(server)) { EndLoading("Server required – please enter name."); MessageBox.Show("Enter server name"); return; }
                UpdateStatus("Opening SQL connection to server...");
                using var conn = new SqlConnection(new SqlConnectionStringBuilder { DataSource = server, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString); await conn.OpenAsync();
                UpdateStatus("Querying online databases (state=0)...");
                using var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE state=0 ORDER BY name", conn); using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync()) _allDatabases.Add(rdr.GetString(0));
                _loadingDatabases = true; try { databasesListBox.DataSource = _allDatabases.ToList(); } finally { _loadingDatabases = false; }
                UpdateDatabaseCount(); exportButton.Enabled = _allDatabases.Count > 0;
                if (_allDatabases.Count == 0) { EndLoading("No online databases found on server."); return; }
                UpdateStatus($"Found {_allDatabases.Count} database(s). Capturing sample snapshots...");
                await CaptureAllDatabaseSnapshotsAsync(server);
                EndLoading("Ready – select a database to inspect its tables.");
            }
            catch (Exception ex) { EndLoading("Connection failed – see error."); MessageBox.Show(ex.Message); }
            finally { connectButton.Enabled = true; }
        }
        private async Task CaptureAllDatabaseSnapshotsAsync(string server)
        {
            if (_allDatabases.Count == 0) return;
            try
            {
                var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExistedDataFolder); Directory.CreateDirectory(folder);
                progressBar.Style = ProgressBarStyle.Continuous; progressBar.Value = 0; progressBar.Maximum = _allDatabases.Count;
                int idx = 0; int created = 0; int skipped = 0;
                foreach (var db in _allDatabases)
                {
                    var canonical = GetCanonicalDbName(db); if (string.IsNullOrWhiteSpace(canonical)) canonical = db;
                    var path = Path.Combine(folder, canonical + SnapshotFileExtension);
                    bool exists = File.Exists(path);
                    UpdateStatus($"Snapshot {idx + 1}/{_allDatabases.Count}: {(exists ? "Skip existing" : "Capture")} '{db}' (canonical '{canonical}')");
                    if (!exists)
                    {
                        await CreateDatabaseSnapshotAsync(server, db, canonical, path); created++; UpdateStatus($"Captured '{db}' sample rows.");
                    }
                    else skipped++;
                    idx++; if (idx <= progressBar.Maximum) progressBar.Value = idx;
                }
                UpdateStatus($"Snapshots done. Created {created}, skipped {skipped}.");
            }
            catch (Exception ex) { UpdateStatus("Snapshot error – see message."); MessageBox.Show(ex.Message); }
            finally { progressBar.Style = ProgressBarStyle.Continuous; }
        }
        private static bool IsIgnoredInferenceColumn(string name)
        { if (string.IsNullOrWhiteSpace(name)) return true; var n = name.ToLowerInvariant(); if (n is "iid" or "rowversion" or "rversion" or "timestamp") return true; if (n.Contains("description") || n.Contains("desc") || n.Contains("notes") || n.Contains("comment")) return true; if (n is "createddate" or "modifieddate" or "updateddate" || n.EndsWith("createdon") || n.EndsWith("updatedon")) return true; if (n == "id") return true; return false; }
        private static bool IsSystemLikeColumnName(string name) { var n = name.ToLowerInvariant(); return n is "iid" or "rowversion" or "rversion" or "timestamp"; }
        private static bool IsNonInsertableType(string type) => type.Equals("timestamp", StringComparison.OrdinalIgnoreCase) || type.Equals("rowversion", StringComparison.OrdinalIgnoreCase);
        private static string QuoteIdentifier(string name) => "[" + name.Replace("]", "]]") + "]";
        private static string SqlLiteral(object? value)
        { if (value == null || value is DBNull) return "NULL"; return value switch { string s => "'" + s.Replace("'", "''") + "'", DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "'", bool b => b ? "1" : "0", byte[] bytes => "0x" + BitConverter.ToString(bytes).Replace("-", string.Empty), Guid g => "'" + g.ToString() + "'", float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL", _ => "'" + value.ToString()?.Replace("'", "''") + "'" }; }
        private static string CsvEscape(object? value)
        { if (value == null || value is DBNull) return string.Empty; string s = value switch { DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture), byte[] b => Convert.ToBase64String(b), _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty }; bool needQuotes = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'); if (needQuotes) s = "\"" + s.Replace("\"", "\"\"") + "\""; return s; }
        private static string FormatElapsed(TimeSpan ts) { if (ts.TotalSeconds < 1) return ts.TotalSeconds.ToString("0.##") + " secs"; int m = (int)ts.TotalMinutes; int s = ts.Seconds; if (m == 0) return s + " sec" + (s == 1 ? "" : "s"); return m + " min" + (m == 1 ? "" : "s") + (s == 0 ? "" : " " + s + " sec" + (s == 1 ? "" : "s")); }
        private static string GenerateRealisticSampleValue(string colName, string sqlType, bool isNullable, int maxLength, byte precision, byte scale, int rowIndex)
        {
            sqlType = sqlType.ToLowerInvariant(); var lower = colName.ToLowerInvariant();
            if (sqlType == "bit") return (rowIndex % 2 == 0 ? "1" : "0");
            if (sqlType == "uniqueidentifier") return "'" + Guid.NewGuid() + "'";
            if (sqlType.Contains("int")) return (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
            if (sqlType.Contains("decimal") || sqlType.Contains("numeric") || sqlType.Contains("money")) { decimal v = (rowIndex + 1) * 1.11m; return v.ToString("F" + scale, CultureInfo.InvariantCulture); }
            if (sqlType.StartsWith("datetime")) return "'" + DateTime.UtcNow.AddMinutes(-rowIndex).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
            bool isText = sqlType.Contains("char") || sqlType.Contains("text") || sqlType.Contains("varchar") || sqlType.Contains("nchar") || sqlType.Contains("nvarchar");
            if (isText)
            {
                int cap = maxLength < 0 ? 64 : (sqlType.StartsWith("n") ? maxLength / 2 : maxLength);
                string value = lower.Contains("name") ? "Name " + (rowIndex + 1) : colName + " " + (rowIndex + 1);
                if (value.Length > cap) value = value.Substring(0, cap);
                value = value.Replace("'", "''");
                return "'" + value + "'";
            }
            return isNullable ? "NULL" : "'Sample'";
        }
        private async Task<Dictionary<(string Schema, string Name), (int inserted, string? error)>> GenerateSampleRowsAsync(SqlConnection conn, IEnumerable<(string Schema, string Name)> emptyTables, int perTableLimit)
        {
            _pendingSampleRows.Clear(); var result = new Dictionary<(string Schema, string Name), (int inserted, string? error)>();
            foreach (var tbl in emptyTables)
            {
                try
                {
                    var metaCols = new List<(string Name, string Type, bool IsIdentity, bool IsComputed, bool IsNullable, int MaxLen, byte Precision, byte Scale)>();
                    using (var meta = new SqlCommand(@"SELECT c.name, ty.name, c.is_identity, c.is_computed, c.is_nullable, c.max_length, c.precision, c.scale FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id AND c.system_type_id=ty.system_type_id WHERE s.name=@s AND t.name=@t", conn))
                    { meta.Parameters.AddWithValue("@s", tbl.Schema); meta.Parameters.AddWithValue("@t", tbl.Name); using var rdr = await meta.ExecuteReaderAsync(); while (await rdr.ReadAsync()) metaCols.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetBoolean(2), rdr.GetBoolean(3), rdr.GetBoolean(4), rdr.GetInt16(5), rdr.GetByte(6), rdr.GetByte(7))); }
                    var insertCols = metaCols.Where(c => !c.IsComputed && !c.IsIdentity && !IsNonInsertableType(c.Type) && !IsSystemLikeColumnName(c.Name)).ToList(); if (insertCols.Count == 0) { result[(tbl.Schema, tbl.Name)] = (0, "No insertable cols"); continue; }
                    var rows = new List<Dictionary<string, string>>();
                    for (int r = 0; r < perTableLimit; r++)
                    {
                        var map = new Dictionary<string, string>();
                        foreach (var c in insertCols)
                        {
                            string val = GenerateRealisticSampleValue(c.Name, c.Type, c.IsNullable, c.MaxLen, c.Precision, c.Scale, r);
                            map[c.Name] = val;
                        }
                        rows.Add(map);
                    }
                    _pendingSampleRows[(tbl.Schema, tbl.Name)] = rows; result[(tbl.Schema, tbl.Name)] = (rows.Count, null);
                }
                catch (Exception ex) { result[(tbl.Schema, tbl.Name)] = (0, ex.Message); }
            }
            return result;
        }
        private async Task<HashSet<string>> GetTransitiveMissingParentsAsync(IEnumerable<TableEntry> selected)
        {
            var selectedSet = selected.Select(t => t.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var edges = await LoadForeignKeyEdgesAsync();
            var parentMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in edges)
            {
                if (!parentMap.TryGetValue(e.Child, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); parentMap[e.Child] = set; }
                set.Add(e.Parent);
            }
            var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in selectedSet)
            {
                var stack = new Stack<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); stack.Push(table);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    if (parentMap.TryGetValue(cur, out var parents))
                    {
                        foreach (var p in parents)
                        {
                            if (seen.Add(p))
                            {
                                if (!selectedSet.Contains(p)) missing.Add(p);
                                stack.Push(p);
                            }
                        }
                    }
                }
            }
            return missing;
        }
        // Foreign key edges loader (single implementation)
        private async Task<List<ForeignKeyEdge>> LoadForeignKeyEdgesAsync()
        {
            var list = new List<ForeignKeyEdge>(); if (_currentDatabase == null) return list;
            try
            {
                var csb = new SqlConnectionStringBuilder { DataSource = serverTextBox.Text.Trim(), InitialCatalog = _currentDatabase, IntegratedSecurity = true, TrustServerCertificate = true };
                using var conn = new SqlConnection(csb.ConnectionString); await conn.OpenAsync();
                const string sql = @"SELECT ps.name AS ParentSchema, pt.name AS ParentTable, pcs.name AS ParentColumn, cs.name AS ChildSchema, ct.name AS ChildTable, ccs.name AS ChildColumn
 FROM sys.foreign_keys fk JOIN sys.tables pt ON fk.referenced_object_id = pt.object_id JOIN sys.schemas ps ON pt.schema_id = ps.schema_id JOIN sys.tables ct ON fk.parent_object_id = ct.object_id JOIN sys.schemas cs ON ct.schema_id = cs.schema_id JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id JOIN sys.columns pcs ON fkc.referenced_object_id = pcs.object_id AND fkc.referenced_column_id = pcs.column_id JOIN sys.columns ccs ON fkc.parent_object_id = ccs.object_id AND fkc.parent_column_id = ccs.column_id";
                using var cmd = new SqlCommand(sql, conn); using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var pFull = rdr.GetString(0) + "." + rdr.GetString(1); var cFull = rdr.GetString(3) + "." + rdr.GetString(4);
                    list.Add(new ForeignKeyEdge(pFull, cFull, rdr.GetString(2), rdr.GetString(5), false));
                }
            }
            catch { }
            foreach (var r in _globalRelationships)
            {
                var parent = r.ParentFull; var child = r.ChildFull;
                bool exists = list.Any(e => e.Parent.Equals(parent, StringComparison.OrdinalIgnoreCase) && e.Child.Equals(child, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ParentColumn, r.ParentColumn, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ChildColumn, r.ChildColumn, StringComparison.OrdinalIgnoreCase));
                if (!exists) list.Add(new ForeignKeyEdge(parent, child, r.ParentColumn, r.ChildColumn, true));
            }
            return list;
        }
        private async void fkGraphButton_Click(object? sender, EventArgs e)
        {
            if (_currentDatabase == null) { MessageBox.Show("Select a database first."); return; }
            BeginLoading("Building FK graph...");
            try
            {
                var edges = await LoadForeignKeyEdgesAsync();
                EndLoading(edges.Count == 0 ? "No relationships" : $"Loaded {edges.Count} edges");
                BuildGraphForm(_currentDatabase!, edges).Show(this);
            }
            catch (Exception ex) { EndLoading("Graph failed"); MessageBox.Show(ex.Message); }
        }
        private Form BuildGraphForm(string db, List<ForeignKeyEdge> edges)
        {
            var frm = new Form { Text = "FK Graph - " + db, Width = 1100, Height = 800, StartPosition = FormStartPosition.CenterParent };
            var canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            int manualCount = edges.Count(e => e.IsManual);
            var info = new Label { Text = $"Relationships: {edges.Count} (Manual: {manualCount})", AutoSize = true, Padding = new Padding(6,8,6,8) };
            var relayout = new Button { Text = "Re-Layout", AutoSize = true };
            top.Controls.Add(info); top.Controls.Add(relayout);
            frm.Controls.Add(canvas); frm.Controls.Add(top);
            var childMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in edges) { if (!childMap.TryGetValue(e.Parent, out var list)) { list = new List<string>(); childMap[e.Parent] = list; } if (!list.Contains(e.Child)) list.Add(e.Child); }
            var nodes = edges.SelectMany(e => new[] { e.Parent, e.Child }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var level = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); ComputeLevels(level, childMap, nodes);
            relayout.Click += (_, __) => { ComputeLevels(level, childMap, nodes); canvas.Invalidate(); };
            canvas.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                const int boxW = 160, boxHeader = 22, colHeight = 14, horizGap = 50, vertGap = 90, margin = 40, maxCols = 20;
                var usedCols = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var ed in edges)
                {
                    if (!string.IsNullOrWhiteSpace(ed.ParentColumn)) { if (!usedCols.TryGetValue(ed.Parent, out var setP)) { setP = new HashSet<string>(StringComparer.OrdinalIgnoreCase); usedCols[ed.Parent] = setP; } setP.Add(ed.ParentColumn!); }
                    if (!string.IsNullOrWhiteSpace(ed.ChildColumn)) { if (!usedCols.TryGetValue(ed.Child, out var setC)) { setC = new HashSet<string>(StringComparer.OrdinalIgnoreCase); usedCols[ed.Child] = setC; } setC.Add(ed.ChildColumn!); }
                }
                var levelGroups = level.GroupBy(k => k.Value).OrderBy(g => g.Key).ToList(); var rects = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
                float maxRight = 0, maxBottom = 0;
                foreach (var g in levelGroups)
                {
                    int idx = 0;
                    foreach (var name in g.Select(x => x.Key).OrderBy(x => x))
                    {
                        var colCount = usedCols.TryGetValue(name, out var set) ? set.Count : 1; float h = boxHeader + Math.Min(colCount, maxCols) * colHeight + 8; var r = new RectangleF(margin + idx * (boxW + horizGap), margin + g.Key * (h + vertGap), boxW, h); rects[name] = r; idx++; if (r.Right > maxRight) maxRight = r.Right; if (r.Bottom > maxBottom) maxBottom = r.Bottom;
                    }
                }
                using var penDb = new Pen(Color.SteelBlue, 2f) { CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 6) };
                using var penManual = new Pen(Color.DarkOrange, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash, CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 6) };
                foreach (var ed in edges)
                {
                    if (!rects.TryGetValue(ed.Parent, out var pr) || !rects.TryGetValue(ed.Child, out var cr)) continue; var from = new PointF(pr.X + pr.Width / 2f, pr.Bottom); var to = new PointF(cr.X + cr.Width / 2f, cr.Top); e.Graphics.DrawLine(ed.IsManual ? penManual : penDb, from, to);
                }
                foreach (var kv in rects)
                {
                    var r = kv.Value; var name = kv.Key; e.Graphics.FillRectangle(Brushes.White, r); e.Graphics.DrawRectangle(Pens.SlateGray, r.X, r.Y, r.Width, r.Height); var headerRect = new RectangleF(r.X, r.Y, r.Width, boxHeader); e.Graphics.FillRectangle(Brushes.AliceBlue, headerRect); e.Graphics.DrawLine(Pens.SlateGray, headerRect.Left, headerRect.Bottom, headerRect.Right, headerRect.Bottom); var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }; e.Graphics.DrawString(name, SystemFonts.DefaultFont, Brushes.Black, headerRect, sf);
                    if (usedCols.TryGetValue(name, out var colsSet) && colsSet.Count > 0)
                    {
                        float y = headerRect.Bottom + 4; int shown = 0; foreach (var c in colsSet.OrderBy(x => x)) { if (shown >= maxCols) { e.Graphics.DrawString("(more...)", SystemFonts.DefaultFont, Brushes.DimGray, r.X + 4, y); break; } e.Graphics.DrawString(c, SystemFonts.DefaultFont, Brushes.Black, r.X + 4, y); y += colHeight; shown++; }
                    }
                }
                canvas.AutoScrollMinSize = new Size((int)(maxRight + margin), (int)(maxBottom + margin));
            };
            return frm;
        }
        private void manageRelationshipsButton_Click(object? sender, EventArgs e)
        {
            // Reuse full UI logic by calling dedicated method if separated; inline simple guard.
            if (_currentDatabase == null) { MessageBox.Show("Select a database first."); return; }
            if (string.IsNullOrWhiteSpace(_currentCanonicalDb)) _currentCanonicalDb = GetCanonicalDbName(_currentDatabase);
            // Invoke full relationship management (same as earlier implementation)
            var dlg = new Form { Text = $"Manage Foreign Keys ({_currentCanonicalDb})", Width = 900, Height = 600, StartPosition = FormStartPosition.CenterParent, MinimumSize = new Size(700,500) };
            var topLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, RowCount = 3, AutoSize = true, Padding = new Padding(8), AutoSizeMode = AutoSizeMode.GrowAndShrink };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            var parentTableLabel = new Label { Text = "Parent Table:", AutoSize = true }; var parentTableCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Left|AnchorStyles.Right, Width = 250 };
            var parentColumnLabel = new Label { Text = "Parent Column:", AutoSize = true }; var parentColumnCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Left|AnchorStyles.Right, Width = 180 };
            var childTableLabel = new Label { Text = "Child Table:", AutoSize = true }; var childTableCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Left|AnchorStyles.Right, Width = 250 };
            var childColumnLabel = new Label { Text = "Child Column:", AutoSize = true }; var childColumnCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Left|AnchorStyles.Right, Width = 180 };
            var typeLabel = new Label { Text = "Rel. Type:", AutoSize = true }; var typeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left }; typeCombo.Items.AddRange(new object[] { "One-to-One", "One-to-Many", "Many-to-One", "Many-to-Many" }); typeCombo.SelectedIndex = 0;
            var addButton = new Button { Text = "Add", AutoSize = true, Anchor = AnchorStyles.Left };
            topLayout.Controls.Add(parentTableLabel,0,0); topLayout.Controls.Add(parentTableCombo,1,0); topLayout.Controls.Add(parentColumnLabel,2,0); topLayout.Controls.Add(parentColumnCombo,3,0);
            topLayout.Controls.Add(childTableLabel,0,1); topLayout.Controls.Add(childTableCombo,1,1); topLayout.Controls.Add(childColumnLabel,2,1); topLayout.Controls.Add(childColumnCombo,3,1);
            topLayout.Controls.Add(typeLabel,0,2); topLayout.Controls.Add(typeCombo,1,2); topLayout.Controls.Add(addButton,3,2);
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            var existingLabel = new Label { Text = "Existing Relationships", Dock = DockStyle.Top, Padding = new Padding(8,4,8,4) };
            var removeInfoLabel = new Label { Text = "Click Remove button in grid to delete", Dock = DockStyle.Top, ForeColor = Color.DimGray, Padding = new Padding(8,0,8,4) };
            var closeBtn = new Button { Text = "Close", Dock = DockStyle.Right, AutoSize = true, DialogResult = DialogResult.OK };
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) }; bottomPanel.Controls.Add(closeBtn);
            dlg.Controls.Add(grid); dlg.Controls.Add(removeInfoLabel); dlg.Controls.Add(existingLabel); dlg.Controls.Add(bottomPanel); dlg.Controls.Add(topLayout);
            var allTables = _allTablesForCurrentDb.Select(t => t.FullName).OrderBy(x => x).ToList(); parentTableCombo.Items.AddRange(allTables.ToArray()); childTableCombo.Items.AddRange(allTables.ToArray());
            var parentCols = new List<string>(); var childCols = new List<string>();
            parentTableCombo.SelectedIndexChanged += (_, __) => { parentCols.Clear(); parentColumnCombo.Items.Clear(); parentColumnCombo.Text = string.Empty; if (parentTableCombo.SelectedItem is string f) { var parts = f.Split('.'); try { using var c = OpenCurrentDb(); parentCols.AddRange(GetColumnsForTable(c, parts[0], parts[1])); } catch { } if (parentCols.Count > 0) parentColumnCombo.Items.AddRange(parentCols.ToArray()); } };
            childTableCombo.SelectedIndexChanged += (_, __) => { childCols.Clear(); childColumnCombo.Items.Clear(); childColumnCombo.Text = string.Empty; if (childTableCombo.SelectedItem is string f) { var parts = f.Split('.'); try { using var c = OpenCurrentDb(); childCols.AddRange(GetColumnsForTable(c, parts[0], parts[1])); } catch { } if (childCols.Count > 0) childColumnCombo.Items.AddRange(childCols.ToArray()); } };
            addButton.Click += (_, __) => AddRelationshipAction(parentTableCombo, parentColumnCombo, childTableCombo, childColumnCombo, typeCombo, grid);
            RefreshRelationshipGrid(grid);
            grid.CellContentClick += (s, ev) => HandleRelationshipGridCellClick(grid, ev);
            if (parentTableCombo.Items.Count > 0) parentTableCombo.SelectedIndex = 0; if (childTableCombo.Items.Count > 0) childTableCombo.SelectedIndex = 0;
            dlg.AcceptButton = addButton; dlg.CancelButton = closeBtn; dlg.ShowDialog(this);
        }
        private void ComputeLevels(Dictionary<string, int> level, Dictionary<string, List<string>> childMap, List<string> nodes)
        {
            level.Clear(); var indeg = nodes.ToDictionary(n => n, n => 0); foreach (var kv in childMap) foreach (var c in kv.Value) indeg[c]++; var q = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key)); while (q.Count > 0) { var n = q.Dequeue(); var cur = level.TryGetValue(n, out var lv) ? lv : 0; if (childMap.TryGetValue(n, out var chs)) { foreach (var c in chs) { var next = cur + 1; if (!level.ContainsKey(c) || level[c] < next) level[c] = next; indeg[c]--; if (indeg[c] == 0) q.Enqueue(c); } } } foreach (var n in nodes) if (!level.ContainsKey(n)) level[n] = 0;
        }
        private void RefreshRelationshipGrid(DataGridView grid)
        {
            grid.Columns.Clear(); grid.Rows.Clear();
            grid.Columns.Add("Parent", "Parent Table"); grid.Columns.Add("ParentColumn", "Parent Column"); grid.Columns.Add("Child", "Child Table"); grid.Columns.Add("ChildColumn", "Child Column"); grid.Columns.Add("Type", "Type");
            var removeCol = new DataGridViewButtonColumn { Name = "Remove", Text = "Remove", HeaderText = "Remove", UseColumnTextForButtonValue = true, Width = 70 }; grid.Columns.Add(removeCol);
            foreach (var r in _globalRelationships.OrderBy(r => r.ParentFull).ThenBy(r => r.ChildFull)) grid.Rows.Add(r.ParentFull, r.ParentColumn, r.ChildFull, r.ChildColumn, r.RelationshipType);
        }
        private void HandleRelationshipGridCellClick(DataGridView grid, DataGridViewCellEventArgs ev)
        {
            if (ev.RowIndex < 0) return; if (grid.Columns[ev.ColumnIndex].Name != "Remove") return;
            var parentFull = grid.Rows[ev.RowIndex].Cells[0].Value?.ToString(); var parentCol = grid.Rows[ev.RowIndex].Cells[1].Value?.ToString(); var childFull = grid.Rows[ev.RowIndex].Cells[2].Value?.ToString(); var childCol = grid.Rows[ev.RowIndex].Cells[3].Value?.ToString(); var type = grid.Rows[ev.RowIndex].Cells[4].Value?.ToString();
            var match = _globalRelationships.FirstOrDefault(r => r.ParentFull == parentFull && r.ParentColumn == parentCol && r.ChildFull == childFull && r.ChildColumn == childCol && r.RelationshipType == type);
            if (match != null) { _globalRelationships.Remove(match); SaveGlobalRelationships(); LoadGlobalRelationships(); RefreshRelationshipGrid(grid); }
        }
        private void AddRelationshipAction(ComboBox parentTableCombo, ComboBox parentColumnCombo, ComboBox childTableCombo, ComboBox childColumnCombo, ComboBox typeCombo, DataGridView grid)
        {
            if (parentTableCombo.SelectedItem is not string p || childTableCombo.SelectedItem is not string c) { MessageBox.Show("Select tables."); return; }
            if (p == c) { MessageBox.Show("Parent and child cannot be same."); return; }
            if (parentColumnCombo.SelectedItem is not string pc || childColumnCombo.SelectedItem is not string cc) { MessageBox.Show("Select columns."); return; }
            var relType = typeCombo.SelectedItem?.ToString() ?? "One-to-One";
            if (_globalRelationships.Any(r => r.ParentFull == p && r.ChildFull == c && r.ParentColumn == pc && r.ChildColumn == cc && r.RelationshipType == relType)) { MessageBox.Show("Duplicate relationship."); return; }
            var pp = p.Split('.'); var cp = c.Split('.'); _globalRelationships.Add(new GlobalRelationship { DatabaseKey = _currentCanonicalDb, ParentSchema = pp[0], ParentTable = pp[1], ParentColumn = pc, ChildSchema = cp[0], ChildTable = cp[1], ChildColumn = cc, RelationshipType = relType });
            SaveGlobalRelationships(); LoadGlobalRelationships(); RefreshRelationshipGrid(grid); MessageBox.Show("Relationship added.");
        }
        private async void autoMapButton_Click(object? sender, EventArgs e)
        {
            if (_currentDatabase == null) { MessageBox.Show("Select a database first."); return; }
            BeginLoading("Scanning columns for suggestions...");
            try
            {
                using var conn = OpenCurrentDb();
                var cols = new List<(string Schema,string Table,string Column,string Type)>();
                const string sql = @"SELECT sch.name, t.name, c.name, ty.name FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id JOIN sys.schemas sch ON t.schema_id = sch.schema_id JOIN sys.types ty ON c.user_type_id = ty.user_type_id AND c.system_type_id = ty.system_type_id";
                using var cmd = new SqlCommand(sql, conn); using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var schema = rdr.GetString(0); var table = rdr.GetString(1); var column = rdr.GetString(2); var type = rdr.GetString(3);
                    if (IsIgnoredInferenceColumn(column) || IsSystemLikeColumnName(column)) continue;
                    if (!string.Equals(type, "int", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "bigint", StringComparison.OrdinalIgnoreCase)) continue;
                    cols.Add((schema, table, column, type));
                }
                var groups = cols.GroupBy(c => (c.Column,c.Type)).Where(g => g.Count() > 1).ToList();
                EndLoading(groups.Count == 0 ? "No suggestions" : $"Found {groups.Count} candidate column groups");
                if (groups.Count == 0) { MessageBox.Show("No matching int/bigint column names across tables."); return; }
                var candidates = new List<(string Parent,string ParentCol,string Child,string ChildCol)>();
                foreach (var g in groups)
                {
                    var list = g.ToList();
                    for (int i=0;i<list.Count;i++)
                        for (int j=0;j<list.Count;j++)
                        { if (i==j) continue; var a=list[i]; var b=list[j]; if (a.Table==b.Table && a.Schema==b.Schema) continue; var p=a.Schema+"."+a.Table; var c=b.Schema+"."+b.Table; if (candidates.Any(x=>x.Parent==p && x.Child==c && x.ParentCol==a.Column && x.ChildCol==b.Column)) continue; candidates.Add((p,a.Column,c,b.Column)); }
                }
                var dlg = new Form { Text = "Suggested Foreign Key Candidates", Width=900, Height=600, StartPosition=FormStartPosition.CenterParent };
                var header = new Label { Text = $"Found {candidates.Count} relationships.", Dock=DockStyle.Top, Height=26, Padding=new Padding(8,4,8,4) };
                var grid = new DataGridView { Dock=DockStyle.Fill, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows=false, AllowUserToDeleteRows=false };
                grid.Columns.Add("Parent","Parent Table"); grid.Columns.Add("ParentCol","Parent Column"); grid.Columns.Add("Child","Child Table"); grid.Columns.Add("ChildCol","Child Column");
                var typeCol = new DataGridViewComboBoxColumn { Name="RelType", HeaderText="Type" }; typeCol.Items.AddRange("One-to-One","One-to-Many","Many-to-One","Many-to-Many"); grid.Columns.Add(typeCol);
                grid.Columns.Add(new DataGridViewCheckBoxColumn { Name="Select", HeaderText="Add", Width=60 });
                foreach (var c in candidates.OrderBy(x=>x.Parent))
                {
                    var existing = _globalRelationships.FirstOrDefault(r=>r.ParentFull==c.Parent && r.ChildFull==c.Child && r.ParentColumn==c.ParentCol && r.ChildColumn==c.ChildCol);
                    grid.Rows.Add(c.Parent,c.ParentCol,c.Child,c.ChildCol, existing?.RelationshipType ?? "One-to-One", existing!=null);
                }
                var buttons = new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=42, FlowDirection=FlowDirection.RightToLeft, Padding=new Padding(6) };
                var saveBtn = new Button { Text="Save Selected", AutoSize=true, DialogResult=DialogResult.OK }; var cancelBtn = new Button { Text="Cancel", AutoSize=true, DialogResult=DialogResult.Cancel }; var selAll = new Button { Text="Select All", AutoSize=true }; var clr = new Button { Text="Clear", AutoSize=true };
                selAll.Click += (_,__) => { foreach (DataGridViewRow r in grid.Rows) r.Cells["Select"].Value = true; }; clr.Click += (_,__) => { foreach (DataGridViewRow r in grid.Rows) r.Cells["Select"].Value = false; };
                buttons.Controls.AddRange(new Control[]{ saveBtn,cancelBtn,clr,selAll });
                dlg.Controls.Add(grid); dlg.Controls.Add(buttons); dlg.Controls.Add(header); dlg.AcceptButton = saveBtn; dlg.CancelButton = cancelBtn;
                if (dlg.ShowDialog(this)==DialogResult.OK)
                {
                    int added=0;
                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        bool sel = r.Cells["Select"].Value is bool b && b; if (!sel) continue;
                        string parentFull = r.Cells["Parent"].Value?.ToString()??""; string parentCol = r.Cells["ParentCol"].Value?.ToString()??""; string childFull = r.Cells["Child"].Value?.ToString()??""; string childCol = r.Cells["ChildCol"].Value?.ToString()??""; string relType = r.Cells["RelType"].Value?.ToString()??"One-to-One";
                        if (string.IsNullOrWhiteSpace(parentFull)||string.IsNullOrWhiteSpace(childFull)||string.IsNullOrWhiteSpace(parentCol)||string.IsNullOrWhiteSpace(childCol)) continue;
                        var pp = parentFull.Split('.'); var cp = childFull.Split('.');
                        if (_globalRelationships.Any(x=>x.ParentSchema==pp[0] && x.ParentTable==pp[1] && x.ChildSchema==cp[0] && x.ChildTable==cp[1] && x.ParentColumn==parentCol && x.ChildColumn==childCol && x.RelationshipType==relType)) continue;
                        _globalRelationships.Add(new GlobalRelationship { DatabaseKey=_currentCanonicalDb, ParentSchema=pp[0], ParentTable=pp[1], ParentColumn=parentCol, ChildSchema=cp[0], ChildTable=cp[1], ChildColumn=childCol, RelationshipType=relType });
                        added++;
                    }
                    if (added>0) { SaveGlobalRelationships(); LoadGlobalRelationships(); MessageBox.Show($"Saved {added} relationship(s)."); } else MessageBox.Show("No relationships saved.");
                }
            }
            catch (Exception ex) { EndLoading("Suggest failed – see error."); MessageBox.Show(ex.Message); }
        }
        private async void refreshSchemaButton_Click(object? sender, EventArgs e)
        {
            if (_currentDatabase == null) { MessageBox.Show("Select a database first."); return; }
            BeginLoading("Refreshing schema...");
            try
 {
                _allTablesForCurrentDb.Clear(); schemaFilterComboBox.Items.Clear(); schemaFilterComboBox.Items.Add("(All)"); schemaFilterComboBox.SelectedIndex = 0;
                using var conn = OpenCurrentDb();
                using var cmd = new SqlCommand(@"SELECT s.name,t.name, ISNULL(SUM(p.rows),0) FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id LEFT JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) GROUP BY s.name,t.name ORDER BY s.name,t.name", conn);
                using var rdr = await cmd.ExecuteReaderAsync(); var schemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await rdr.ReadAsync()) { var te = new TableEntry(rdr.GetString(0), rdr.GetString(1), rdr.GetInt64(2)); _allTablesForCurrentDb.Add(te); schemas.Add(te.Schema); }
                foreach (var s in schemas.OrderBy(x=>x)) schemaFilterComboBox.Items.Add(s);
                ApplySchemaTableFilter(); selectedDatabaseLabel.Text = $"Selected Database: {_currentDatabase} | Canonical: {_currentCanonicalDb} | Tables: {_allTablesForCurrentDb.Count}";
                EndLoading(_allTablesForCurrentDb.Count==0?"No tables":"Tables refreshed.");
            }
            catch (Exception ex) { EndLoading("Refresh failed"); MessageBox.Show(ex.Message); return; }
            try { await InferColumnDependenciesAsync(serverTextBox.Text.Trim(), _currentDatabase); } catch { }
            LoadGlobalRelationships();
        }
        private async void exportButton_Click(object? sender, EventArgs e)
        {
            var db = _currentDatabase; if (db == null) { MessageBox.Show("Select a database first."); return; }
            var selected = tablesCheckedListBox.CheckedItems.Cast<TableEntry>().ToList(); if (selected.Count == 0) { MessageBox.Show("Select at least one table."); return; }
            var missingParents = await GetTransitiveMissingParentsAsync(selected);
            if (missingParents.Count > 0)
            {
                var preview = string.Join("\n", missingParents.Take(25)) + (missingParents.Count > 25 ? "\n..." : string.Empty);
                var dr = MessageBox.Show($"You selected {selected.Count} table(s) but {missingParents.Count} parent table(s) are not selected:\n\n{preview}\n\nInclude them?", "Include Parent Tables?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dr == DialogResult.Cancel) return;
                if (dr == DialogResult.Yes)
                {
                    var selectedNames = selected.Select(t => t.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var add in _allTablesForCurrentDb.Where(t => missingParents.Contains(t.FullName)))
                    {
                        if (!selectedNames.Contains(add.FullName))
                        {
                            selected.Add(add); selectedNames.Add(add.FullName);
                            for (int i = 0; i < tablesCheckedListBox.Items.Count; i++)
                                if (tablesCheckedListBox.Items[i] is TableEntry te && te.FullName.Equals(add.FullName, StringComparison.OrdinalIgnoreCase))
                                    tablesCheckedListBox.SetItemChecked(i, true);
                        }
                    }
                }
            }
            using var fbd = new FolderBrowserDialog { Description = "Select output folder" }; if (fbd.ShowDialog(this) != DialogResult.OK) return; var outDir = fbd.SelectedPath; if (string.IsNullOrWhiteSpace(outDir)) return;
            exportButton.Enabled = false; connectButton.Enabled = false; BeginLoading("Preparing export ordering..."); var sw = Stopwatch.StartNew();
            try
            {
                var ordered = OrderByInferredDependencies(selected); UpdateStatus($"Ordered {ordered.Count} tables by dependency.");
                var rowsMode = rowsComboBox.SelectedItem?.ToString() ?? "All Data";
                bool first = rowsMode.StartsWith("First", StringComparison.OrdinalIgnoreCase); bool last = rowsMode.StartsWith("Last", StringComparison.OrdinalIgnoreCase);
                int limit = 0; if (first||last){ var digits = new string(rowsMode.Where(char.IsDigit).ToArray()); if(int.TryParse(digits,out var parsed)) limit = parsed; }
                int sampleCount = (first||last) && limit>0 ? limit : 10;
                using var conn = new SqlConnection(new SqlConnectionStringBuilder { DataSource = serverTextBox.Text.Trim(), InitialCatalog = db, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true }.ConnectionString); await conn.OpenAsync(); UpdateStatus("Connected – preparing synthetic/sample rows...");
                var emptySelected = ordered.Where(t=>t.RowCount==0).Select(t=>(t.Schema,t.Name)).ToHashSet();
                if (emptySelected.Count>0)
                { UpdateStatus($"Loading synthetic rows for {emptySelected.Count} empty table(s)..."); await PopulateSyntheticDataForEmptyTablesAsync(conn, emptySelected); var missing = emptySelected.Where(e=>!_pendingSampleRows.ContainsKey(e)).ToHashSet(); if (missing.Count>0){ UpdateStatus($"Generating sample values for {missing.Count} table(s)..."); await GenerateSampleRowsAsync(conn, missing, sampleCount);} }
                Directory.CreateDirectory(outDir); UpdateStatus("Writing INSERT scripts to disk...");
                await ExportSelectedTablesAsync(conn, outDir, ordered, first, last, limit, rowsMode);
                sw.Stop(); EndLoading($"Export complete – {ordered.Count} table(s) in {FormatElapsed(sw.Elapsed)}."); MessageBox.Show($"Exported {ordered.Count} table(s).", "SQL Export Complete");
            }
            catch (Exception ex) { EndLoading("Export failed – see error."); MessageBox.Show(ex.Message); }
            finally { exportButton.Enabled = true; connectButton.Enabled = true; }
        }
        // Restore missing helper field
        private readonly SyntheticDataService _syntheticDataService = new SyntheticDataService();
        // Restore layout helper
        private void LayoutTopControls()
        {
            try
            {
                if (autoMapButton == null || manageRelationshipsButton == null || fkGraphButton == null || schemaFilterComboBox == null || schemaFilterLabel == null || selectAllTablesButton == null || clearTablesButton == null) return;
                int gap = 8; int top = 229; int leftMargin = 12; int rightMargin = 12; int right = ClientSize.Width - rightMargin;
                clearTablesButton.Location = new Point(right - clearTablesButton.Width, top + 3); right = clearTablesButton.Left - gap;
                selectAllTablesButton.Location = new Point(right - selectAllTablesButton.Width, top + 3); right = selectAllTablesButton.Left - gap;
                if (refreshSchemaButton != null) { refreshSchemaButton.Location = new Point(right - refreshSchemaButton.Width, top + 3); right = refreshSchemaButton.Left - gap; }
                int left = leftMargin;
                autoMapButton.Location = new Point(left, top); left = autoMapButton.Right + gap;
                manageRelationshipsButton.Location = new Point(left, top); left = manageRelationshipsButton.Right + gap;
                fkGraphButton.Location = new Point(left, top); left = fkGraphButton.Right + gap;
                schemaFilterLabel.Location = new Point(left, top + 5);
                int comboLeft = schemaFilterLabel.Right + 6; int comboWidth = Math.Min(140, Math.Max(100, right - comboLeft));
                schemaFilterComboBox.Location = new Point(comboLeft, top + 2); schemaFilterComboBox.Width = comboWidth;
            }
            catch { }
        }
        // Restore schema filter apply
        private void ApplySchemaTableFilter()
        {
            try
            {
                var selectedSchema = schemaFilterComboBox.SelectedItem as string;
                var filtered = string.IsNullOrEmpty(selectedSchema) || selectedSchema == "(All)" ? _allTablesForCurrentDb : _allTablesForCurrentDb.Where(t => t.Schema.Equals(selectedSchema, StringComparison.OrdinalIgnoreCase)).ToList();
                tablesCheckedListBox.Items.Clear(); foreach (var t in filtered) tablesCheckedListBox.Items.Add(t, true);
            }
            catch { }
        }
        // Restore schema filter combo event handler
        private void schemaFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        { if (_currentDatabase == null) return; ApplySchemaTableFilter(); }
        // Restore DB list selection changed async handler
        private async void databasesListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_loadingDatabases) return; var db = databasesListBox.SelectedItem as string;
            tablesCheckedListBox.Items.Clear(); exportButton.Enabled = false; _currentDatabase = null; schemaFilterComboBox.Items.Clear(); schemaFilterComboBox.Items.Add("(All)"); schemaFilterComboBox.SelectedIndex = 0; _allTablesForCurrentDb.Clear();
            if (string.IsNullOrWhiteSpace(db)) { selectedDatabaseLabel.Text = "Selected Database: (none)"; return; }
            _currentDatabase = db; _currentCanonicalDb = GetCanonicalDbName(db);
            BeginLoading($"Loading tables for '{db}'...");
            try
            {
                using var conn = new SqlConnection(new SqlConnectionStringBuilder { DataSource = serverTextBox.Text.Trim(), InitialCatalog = db, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true }.ConnectionString); await conn.OpenAsync();
                UpdateStatus("Querying table list and row counts...");
                using var cmd = new SqlCommand(@"SELECT s.name,t.name, ISNULL(SUM(p.rows),0) FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id LEFT JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) GROUP BY s.name,t.name ORDER BY s.name,t.name", conn);
                using var rdr = await cmd.ExecuteReaderAsync(); var schemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long empty = 0;
                while (await rdr.ReadAsync()) { var rc = rdr.GetInt64(2); if (rc == 0) empty++; var te = new TableEntry(rdr.GetString(0), rdr.GetString(1), rc); _allTablesForCurrentDb.Add(te); schemas.Add(te.Schema); }
                foreach (var s in schemas.OrderBy(x => x)) schemaFilterComboBox.Items.Add(s);
                ApplySchemaTableFilter(); selectedDatabaseLabel.Text = $"Selected Database: {db} | Canonical: {_currentCanonicalDb} | Tables: {_allTablesForCurrentDb.Count}";
                EndLoading(_allTablesForCurrentDb.Count == 0 ? "No tables found." : $"Loaded {_allTablesForCurrentDb.Count} tables ({empty} empty)." ); exportButton.Enabled = _allTablesForCurrentDb.Count > 0;
            }
            catch (Exception ex) { EndLoading("Table load failed – see error."); MessageBox.Show(ex.Message); }
            try { UpdateStatus("Inferring column dependencies..."); await InferColumnDependenciesAsync(serverTextBox.Text.Trim(), db); UpdateStatus($"Inference complete – {_inferredColumnDeps.Count} possible dependencies."); } catch { UpdateStatus("Inference skipped due to error."); }
            LoadGlobalRelationships(); UpdateStatus($"Ready – loaded {_globalRelationships.Count} manual/global relationship(s).");
        }
        // Restore snapshot creation
        private async Task CreateDatabaseSnapshotAsync(string server, string database, string canonical, string snapshotPath)
        {
            try
            {
                var csb = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true };
                using var conn = new SqlConnection(csb.ConnectionString); await conn.OpenAsync();
                var tables = new List<(string Schema,string Name,long Rows)>();
                using (var cmd = new SqlCommand(@"SELECT s.name,t.name, ISNULL(SUM(p.rows),0) AS RowCnt FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id LEFT JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) GROUP BY s.name,t.name ORDER BY s.name,t.name", conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                { while (await rdr.ReadAsync()) tables.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetInt64(2))); }
                var snapshot = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (schema,name,rowCount) in tables)
                {
                    if (rowCount == 0) continue;
                    var cols = new List<string>();
                    using (var colCmd = new SqlCommand(@"SELECT c.name, ty.name, c.is_identity, c.is_computed FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id AND c.system_type_id=ty.system_type_id WHERE s.name=@s AND t.name=@t ORDER BY c.column_id", conn))
                    {
                        colCmd.Parameters.AddWithValue("@s", schema); colCmd.Parameters.AddWithValue("@t", name);
                        using var cr = await colCmd.ExecuteReaderAsync();
                        while (await cr.ReadAsync())
                        {
                            var colName = cr.GetString(0); var typeName = cr.GetString(1); bool isIdentity = cr.GetBoolean(2); bool isComputed = cr.GetBoolean(3);
                            if (isIdentity || isComputed) continue;
                            if (typeName.Equals("timestamp", StringComparison.OrdinalIgnoreCase) || typeName.Equals("rowversion", StringComparison.OrdinalIgnoreCase)) continue;
                            if (IsSystemLikeColumnName(colName)) continue;
                            cols.Add(colName);
                        }
                    }
                    if (cols.Count == 0) continue;
                    string colList = string.Join(", ", cols.Select(QuoteIdentifier));
                    var fullNameQuoted = QuoteIdentifier(schema) + "." + QuoteIdentifier(name);
                    var sql = rowCount <= SnapshotRowLimit ? $"SELECT {colList} FROM {fullNameQuoted}" : $"SELECT TOP({SnapshotRowLimit}) {colList} FROM {fullNameQuoted} ORDER BY (SELECT NULL)";
                    using var dataCmd = new SqlCommand(sql, conn);
                    try
                    {
                        using var rdr2 = await dataCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                        var rows = new List<Dictionary<string, object?>>();
                        while (rows.Count < SnapshotRowLimit && await rdr2.ReadAsync())
                        {
                            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < rdr2.FieldCount; i++) map[rdr2.GetName(i)] = rdr2.IsDBNull(i) ? null : rdr2.GetValue(i);
                            rows.Add(map);
                        }
                        if (rows.Count > 0) snapshot[$"{schema}.{name}"] = rows;
                    }
                    catch { }
                }
                if (snapshot.Count > 0)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(snapshotPath, json);
                }
            }
            catch { }
        }
        // Restore synthetic data population for empty tables
        private async Task PopulateSyntheticDataForEmptyTablesAsync(SqlConnection conn, HashSet<(string Schema,string Name)> emptyTables)
        {
            if (emptyTables.Count == 0 || string.IsNullOrWhiteSpace(_currentCanonicalDb)) return;
            try
            {
                var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExistedDataFolder);
                var synthetic = _syntheticDataService.LoadSyntheticRowsForEmptyTables(baseDir, _currentCanonicalDb, emptyTables, conn);
                foreach (var kv in synthetic) _pendingSampleRows[kv.Key] = kv.Value; // values already SQL literals
            }
            catch { }
        }
        // Restore export helper
        private async Task<List<ExportResult>> ExportSelectedTablesAsync(SqlConnection conn, string outputDir, List<TableEntry> entries, bool first, bool last, int limit, string rowsMode)
        {
            var results = new List<ExportResult>(entries.Count);
            progressBar.Visible = true; progressBar.Style = ProgressBarStyle.Continuous; progressBar.Value = 0; progressBar.Maximum = entries.Count == 0 ? 1 : entries.Count;
            for (int idx = 0; idx < entries.Count; idx++)
            {
                var te = entries[idx];
                int current = idx + 1; int total = entries.Count; int percent = (int)Math.Round(current * 100.0 / (total == 0 ? 1 : total));
                UpdateStatus($"Processing {te.FullName} ({current}/{total}, {percent}%)");
                await Task.Yield();
                if (te.RowCount == 0 && !_pendingSampleRows.ContainsKey((te.Schema, te.Name)))
                {
                    var singleSet = new HashSet<(string Schema,string Name)> { (te.Schema, te.Name) };
                    await PopulateSyntheticDataForEmptyTablesAsync(conn, singleSet);
                }
                var file = Path.Combine(outputDir, te.FullName.Replace('.', '_') + ".sql"); int sampleCount = 0; int dataCount = 0; string? error = null;
                try
                {
                    using var writer = new StreamWriter(file, false, Encoding.UTF8);
                    await writer.WriteLineAsync($"-- Export {te.FullName} Mode={rowsMode} UTC={DateTime.UtcNow:O}");
                    if (_pendingSampleRows.TryGetValue((te.Schema, te.Name), out var sampleRows) && sampleRows.Count > 0)
                    {
                        var colsS = sampleRows[0].Keys.ToList(); var colListS = string.Join(", ", colsS.Select(QuoteIdentifier)); var fullNameS = QuoteIdentifier(te.Schema) + "." + QuoteIdentifier(te.Name);
                        foreach (var row in sampleRows)
                        {
                            var vals = string.Join(", ", colsS.Select(c => row[c]));
                            await writer.WriteLineAsync($"INSERT INTO {fullNameS} ({colListS}) VALUES ({vals});");
                        }
                        sampleCount = sampleRows.Count;
                    }
                    var insertableCols = new List<string>();
                    using (var colCmd = new SqlCommand(@"SELECT c.name FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id AND c.system_type_id=ty.system_type_id WHERE s.name=@s AND t.name=@t AND c.is_identity=0 AND c.is_computed=0 AND ty.name NOT IN('timestamp','rowversion') ORDER BY c.column_id", conn))
                    { colCmd.Parameters.AddWithValue("@s", te.Schema); colCmd.Parameters.AddWithValue("@t", te.Name); using var rdr = await colCmd.ExecuteReaderAsync(); while (await rdr.ReadAsync()) insertableCols.Add(rdr.GetString(0)); }
                    if (insertableCols.Count > 0)
                    {
                        string columnList = string.Join(", ", insertableCols.Select(QuoteIdentifier)); string fullName = QuoteIdentifier(te.Schema) + "." + QuoteIdentifier(te.Name); string selectSql;
                        if (first && limit > 0) selectSql = $"SELECT TOP({limit}) {columnList} FROM {fullName} ORDER BY (SELECT NULL)";
                        else if (last && limit > 0) selectSql = $"SELECT {columnList} FROM (SELECT {columnList}, ROW_NUMBER() OVER(ORDER BY(SELECT NULL)) rn, COUNT(*) OVER() total FROM {fullName}) z WHERE rn > total - {limit} ORDER BY rn";
                        else selectSql = $"SELECT {columnList} FROM {fullName}";
                        using var dataCmd = new SqlCommand(selectSql, conn);
                        using var dr = await dataCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                        while (await dr.ReadAsync())
                        {
                            var sb = new StringBuilder();
                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                sb.Append(SqlLiteral(dr.GetValue(i)));
                                if (i < dr.FieldCount - 1) sb.Append(", ");
                            }
                            await writer.WriteLineAsync($"INSERT INTO {fullName} ({columnList}) VALUES ({sb});"); dataCount++;
                        }
                    }
                }
                catch (Exception ex) { error = ex.Message; }
                results.Add(new ExportResult { Table = te.FullName, SampleRows = sampleCount, DataRows = dataCount, Error = error });
                if (current <= progressBar.Maximum) progressBar.Value = current;
            }
            UpdateStatus("Export phase complete");
            return results;
        }
        // Restore CSV export (basic)
        private async void exportCsvButton_Click(object? sender, EventArgs e)
        {
            var db = _currentDatabase; if (db == null) { MessageBox.Show("Select a database first."); return; }
            var selected = tablesCheckedListBox.CheckedItems.Cast<TableEntry>().ToList(); if (selected.Count == 0) { MessageBox.Show("Select at least one table."); return; }
            using var fbd = new FolderBrowserDialog { Description = "Select output folder for CSV" }; if (fbd.ShowDialog(this) != DialogResult.OK) return; var outDir = fbd.SelectedPath; if (string.IsNullOrWhiteSpace(outDir)) return;
            exportCsvButton.Enabled = false; BeginLoading("Starting CSV export..."); var sw = Stopwatch.StartNew();
            try
            {
                var ordered = OrderByInferredDependencies(selected);
                using var conn = new SqlConnection(new SqlConnectionStringBuilder { DataSource = serverTextBox.Text.Trim(), InitialCatalog = db, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true }.ConnectionString); await conn.OpenAsync();
                Directory.CreateDirectory(outDir);
                int idx = 0; progressBar.Value = 0; progressBar.Maximum = ordered.Count == 0 ? 1 : ordered.Count; progressBar.Style = ProgressBarStyle.Continuous;
                foreach (var te in ordered)
                {
                    idx++; int percent = (int)Math.Round(idx * 100.0 / (ordered.Count == 0 ? 1 : ordered.Count)); UpdateStatus($"CSV {idx}/{ordered.Count} ({percent}%) – {te.FullName}");
                    var file = Path.Combine(outDir, te.FullName.Replace('.', '_') + ".csv");
                    using var writer = new StreamWriter(file, false, Encoding.UTF8);
                    var insertableCols = new List<string>();
                    using (var colCmd = new SqlCommand(@"SELECT c.name FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id AND c.system_type_id=ty.system_type_id WHERE s.name=@s AND t.name=@t AND c.is_identity=0 AND c.is_computed=0 AND ty.name NOT IN('timestamp','rowversion') ORDER BY c.column_id", conn))
                    { colCmd.Parameters.AddWithValue("@s", te.Schema); colCmd.Parameters.AddWithValue("@t", te.Name); using var rdr = await colCmd.ExecuteReaderAsync(); while (await rdr.ReadAsync()) insertableCols.Add(rdr.GetString(0)); }
                    if (insertableCols.Count == 0) { await writer.WriteLineAsync("# No insertable columns"); continue; }
                    await writer.WriteLineAsync(string.Join(',', insertableCols));
                    string columnList = string.Join(", ", insertableCols.Select(QuoteIdentifier)); string fullName = QuoteIdentifier(te.Schema) + "." + QuoteIdentifier(te.Name); string selectSql = $"SELECT {columnList} FROM {fullName}";
                    using var dataCmd = new SqlCommand(selectSql, conn); using var rdr2 = await dataCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                    while (await rdr2.ReadAsync())
                    {
                        var cells = new string[rdr2.FieldCount];
                        for (int i = 0; i < rdr2.FieldCount; i++) cells[i] = CsvEscape(rdr2.GetValue(i));
                        await writer.WriteLineAsync(string.Join(',', cells));
                    }
                    if (idx <= progressBar.Maximum) progressBar.Value = idx;
                }
                sw.Stop(); EndLoading($"CSV export complete – {ordered.Count} table(s) in {FormatElapsed(sw.Elapsed)}."); MessageBox.Show($"Exported {ordered.Count} table(s) to CSV.");
            }
            catch (Exception ex) { EndLoading("CSV export failed – see error."); MessageBox.Show(ex.Message); }
            finally { exportCsvButton.Enabled = true; }
        }
    }
}