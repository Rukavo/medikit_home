using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace HomePharma.Core;

public sealed class PharmaStore(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    private static void Execute(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object)[] args)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value); cmd.ExecuteNonQuery();
    }
    public async Task InitializeAsync(Func<Task<string>> seed)
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var db = Open();
            Execute(db, null, "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS medications(id TEXT PRIMARY KEY, payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS categories(name TEXT PRIMARY KEY COLLATE NOCASE); CREATE TABLE IF NOT EXISTS metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL); PRAGMA user_version=1;");
            using var check = db.CreateCommand(); check.CommandText = "SELECT COUNT(*) FROM metadata WHERE key='initialized'";
            if ((long)check.ExecuteScalar()! > 0) return;
            var seedText = await seed();
            var initial = seedText.Trim().Contains('\n') ? CsvCodec.Import(seedText) : new CsvImport([], [], []);
            if (initial.Errors.Count > 0) throw new FormatException(string.Join("\n", initial.Errors));
            using var tx = db.BeginTransaction();
            foreach (var med in initial.Medications) Save(db, tx, med);
            Execute(db, tx, "INSERT INTO metadata VALUES('initialized','1')"); tx.Commit();
        }
        finally { gate.Release(); }
    }
    public async Task<List<Medication>> AllAsync()
    {
        await gate.WaitAsync(); try { using var db = Open(); return ReadAll(db); } finally { gate.Release(); }
    }
    private static List<Medication> ReadAll(SqliteConnection db)
    {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT payload FROM medications"; using var reader = cmd.ExecuteReader();
        List<Medication> result = []; while (reader.Read()) result.Add(JsonSerializer.Deserialize(reader.GetString(0), PharmaJson.Default.Medication)!); return result;
    }
    private static void Save(SqliteConnection db, SqliteTransaction tx, Medication med)
    {
        med.Validate();
        Execute(db, tx, "INSERT INTO medications(id,payload) VALUES($id,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", ("$id", med.Id), ("$payload", JsonSerializer.Serialize(med, PharmaJson.Default.Medication)));
        foreach (var category in med.Categories) Execute(db, tx, "INSERT OR IGNORE INTO categories VALUES($name)", ("$name", category));
    }
    public async Task SaveAsync(Medication med)
    {
        await gate.WaitAsync(); try { using var db = Open(); using var tx = db.BeginTransaction(); Save(db, tx, med); tx.Commit(); } finally { gate.Release(); }
    }
    public async Task DeleteAsync(string id)
    {
        await gate.WaitAsync(); try { using var db = Open(); Execute(db, null, "DELETE FROM medications WHERE id=$id", ("$id", id)); } finally { gate.Release(); }
    }
    public async Task<int> ImportAsync(CsvImport batch)
    {
        if (batch.Errors.Count > 0) throw new FormatException("Corrige todos los errores del CSV antes de importar.");
        await gate.WaitAsync();
        try
        {
            using var db = Open(); var existing = ReadAll(db); var ids = existing.Select(m => m.Id).ToHashSet(); var keys = existing.Select(m => m.Fingerprint).ToHashSet();
            using var tx = db.BeginTransaction(); int count = 0;
            foreach (var med in batch.Medications)
            {
                med.Validate();
                if (ids.Contains(med.Id) || keys.Contains(med.Fingerprint)) continue;
                Save(db, tx, med); ids.Add(med.Id); keys.Add(med.Fingerprint); count++;
            }
            tx.Commit(); return count;
        }
        finally { gate.Release(); }
    }
    public async Task<List<string>> CategoriesAsync()
    {
        await gate.WaitAsync(); try
        {
            using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT name FROM categories ORDER BY name COLLATE NOCASE";
            using var reader = cmd.ExecuteReader(); List<string> result = []; while (reader.Read()) result.Add(reader.GetString(0)); return result;
        } finally { gate.Release(); }
    }
    public async Task AddCategoryAsync(string name)
    {
        var test = new Medication { Name = "Category validation", Categories = [name] }; test.Validate();
        if (test.Categories.Count != 1) throw new FormatException("Escribe un nombre de categoría.");
        await gate.WaitAsync(); try { using var db = Open(); Execute(db, null, "INSERT OR IGNORE INTO categories VALUES($name)", ("$name", test.Categories[0])); } finally { gate.Release(); }
    }
    public async Task ChangeCategoryAsync(string oldName, string? newName)
    {
        if (newName != null) { var test = new Medication { Name = "Category validation", Categories = [newName] }; test.Validate(); if (test.Categories.Count != 1) throw new FormatException("Escribe un nombre de categoría."); newName = test.Categories[0]; }
        await gate.WaitAsync(); try
        {
            using var db = Open(); var meds = ReadAll(db); using var tx = db.BeginTransaction();
            foreach (var med in meds.Where(m => m.Categories.Contains(oldName, StringComparer.OrdinalIgnoreCase)))
            {
                med.Categories.RemoveAll(c => c.Equals(oldName, StringComparison.OrdinalIgnoreCase)); if (newName != null) med.Categories.Add(newName); Save(db, tx, med);
            }
            Execute(db, tx, "DELETE FROM categories WHERE name=$name", ("$name", oldName));
            if (newName != null) Execute(db, tx, "INSERT OR IGNORE INTO categories VALUES($name)", ("$name", newName)); tx.Commit();
        } finally { gate.Release(); }
    }
    public async Task<string> BackupAsync()
    {
        await gate.WaitAsync();
        try
        {
            using var db = Open(); using var tx = db.BeginTransaction();
            var medications = ReadAllInTransaction(db, tx);
            using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "SELECT name FROM categories ORDER BY name COLLATE NOCASE";
            List<string> categories = []; using (var reader = cmd.ExecuteReader()) { while (reader.Read()) categories.Add(reader.GetString(0)); }
            tx.Commit();
            return JsonSerializer.Serialize(new Backup(1, DateTimeOffset.UtcNow, medications, categories), PharmaJson.Default.Backup);
        }
        finally { gate.Release(); }
    }
    private static List<Medication> ReadAllInTransaction(SqliteConnection db, SqliteTransaction tx)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "SELECT payload FROM medications"; using var reader = cmd.ExecuteReader();
        List<Medication> result = []; while (reader.Read()) result.Add(JsonSerializer.Deserialize(reader.GetString(0), PharmaJson.Default.Medication)!); return result;
    }
    public static Backup ReadBackup(string json)
    {
        var backup = JsonSerializer.Deserialize(json, PharmaJson.Default.Backup) ?? throw new FormatException("Copia no válida.");
        if (backup.Version != 1 || backup.Medications is null || backup.Categories is null) throw new FormatException("Versión de copia no compatible.");
        foreach (var med in backup.Medications) med.Validate();
        if (backup.Medications.Select(m => m.Id).Distinct().Count() != backup.Medications.Count) throw new FormatException("La copia contiene identificadores repetidos.");
        foreach (var category in backup.Categories) { var test = new Medication { Name = "Category", Categories = [category] }; test.Validate(); if (test.Categories.Count != 1) throw new FormatException("Categoría vacía en la copia."); }
        return backup;
    }
    public async Task RestoreAsync(Backup backup)
    {
        ReadBackup(JsonSerializer.Serialize(backup, PharmaJson.Default.Backup)); await gate.WaitAsync(); try
        {
            using var db = Open(); using var tx = db.BeginTransaction(); Execute(db, tx, "DELETE FROM medications; DELETE FROM categories;");
            foreach (var med in backup.Medications) Save(db, tx, med);
            foreach (var category in backup.Categories) Execute(db, tx, "INSERT OR IGNORE INTO categories VALUES($name)", ("$name", category.Trim())); tx.Commit();
        } finally { gate.Release(); }
    }
}
public sealed record Backup(int Version, DateTimeOffset CreatedAt, List<Medication> Medications, List<string> Categories);
