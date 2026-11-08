using System.Text.Json;
using Microsoft.Data.Sqlite;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.Persistence;
public sealed class Store
{
    readonly string connectionString;
    public string DatabasePath {get;}
    public Store(string path) { DatabasePath=Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); connectionString = new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 10 }.ToString(); }
    SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void Initialize()
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = """
        PRAGMA journal_mode=WAL;
        PRAGMA busy_timeout=10000;
        CREATE TABLE IF NOT EXISTS schema_migrations(version INTEGER PRIMARY KEY);
        CREATE TABLE IF NOT EXISTS probes(id INTEGER PRIMARY KEY, at TEXT NOT NULL, target TEXT NOT NULL, data TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_probes_at ON probes(at);
        CREATE INDEX IF NOT EXISTS ix_probes_target_at ON probes(target,at);
        CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY, start TEXT NOT NULL, finish TEXT, data TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS incidents(id TEXT PRIMARY KEY, start TEXT NOT NULL, data TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS network_events(id INTEGER PRIMARY KEY, at TEXT NOT NULL, data TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS settings(id INTEGER PRIMARY KEY CHECK(id=1), data TEXT NOT NULL);
        INSERT OR IGNORE INTO schema_migrations VALUES(1);
        """; cmd.ExecuteNonQuery();
        using var migration=c.BeginTransaction();
        using var check=c.CreateCommand();check.Transaction=migration;check.CommandText="SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name='baseline'";
        if((long)check.ExecuteScalar()! == 0)
        {
            using var alter=c.CreateCommand();alter.Transaction=migration;alter.CommandText="ALTER TABLE sessions ADD COLUMN baseline TEXT";alter.ExecuteNonQuery();
            using var read=c.CreateCommand();read.Transaction=migration;read.CommandText="SELECT data FROM sessions";
            var sessions=new List<Session>();using(var reader=read.ExecuteReader()){while(reader.Read())sessions.Add(JsonSerializer.Deserialize<Session>(reader.GetString(0))!);}
            foreach(var session in sessions){using var update=c.CreateCommand();update.Transaction=migration;update.CommandText="UPDATE sessions SET baseline=$baseline WHERE id=$id";update.Parameters.AddWithValue("$baseline",Stamp(session.BaselineStart));update.Parameters.AddWithValue("$id",session.Id);update.ExecuteNonQuery();}
        }
        using var mark=c.CreateCommand();mark.Transaction=migration;mark.CommandText="INSERT OR IGNORE INTO schema_migrations VALUES(2)";mark.ExecuteNonQuery();migration.Commit();
    }
    public static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O");
    public void WriteProbes(IEnumerable<Probe> probes)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var p in probes) { using var cmd = c.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO probes(at,target,data) VALUES($at,$target,$data)";
            cmd.Parameters.AddWithValue("$at", Stamp(p.At)); cmd.Parameters.AddWithValue("$target", p.TargetId);
            cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(p)); cmd.ExecuteNonQuery(); }
        tx.Commit();
    }
    public List<Probe> ReadProbes(DateTimeOffset start, DateTimeOffset end) => Read<Probe>("SELECT data FROM probes WHERE at >= $start AND at < $end ORDER BY at,id", start, end);
    public List<NetworkEvent> ReadEvents(DateTimeOffset start, DateTimeOffset end) => Read<NetworkEvent>("SELECT data FROM network_events WHERE at >= $start AND at < $end ORDER BY at,id", start, end);
    public List<Incident> ReadIncidents(DateTimeOffset start, DateTimeOffset end) => Read<Incident>("SELECT data FROM incidents WHERE start < $end", start, end).Where(i => i.End == null || i.End >= start).ToList();
    List<T> Read<T>(string sql, DateTimeOffset start, DateTimeOffset end)
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$start", Stamp(start)); cmd.Parameters.AddWithValue("$end", Stamp(end));
        using var reader = cmd.ExecuteReader(); var result = new List<T>(); while (reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0))!); return result;
    }
    public List<Session> Sessions() => Read<Session>("SELECT data FROM sessions ORDER BY start DESC", DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
    public void Save(Session s) => Execute("INSERT OR REPLACE INTO sessions(id,start,finish,data,baseline) VALUES($id,$at,$end,$data,$baseline)", s.Id, s.Start, s, s.End);
    public void Save(Incident i) => Execute("INSERT OR REPLACE INTO incidents(id,start,data) VALUES($id,$at,$data)", i.Id, i.Start, i);
    public void Save(NetworkEvent e) => Execute("INSERT INTO network_events(at,data) VALUES($at,$data)", "", e.At, e);
    void Execute(string sql, string id, DateTimeOffset at, object data, DateTimeOffset? end = null)
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$baseline",data is Session session ? Stamp(session.BaselineStart) : DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id); cmd.Parameters.AddWithValue("$at", Stamp(at));
        cmd.Parameters.AddWithValue("$end", end is null ? DBNull.Value : Stamp(end.Value)); cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(data, data.GetType())); cmd.ExecuteNonQuery();
    }
    public void Recover(DateTimeOffset now)
    {
        foreach (var s in Sessions().Where(s => s.End is null))
        {
            var last = ReadProbes(s.Start, now).LastOrDefault()?.At ?? s.Start;
            Save(s with { End = last, State = "Incomplète — récupérée après interruption" });
        }
        foreach (var i in ReadIncidents(DateTimeOffset.MinValue, now).Where(i => i.End is null)) Save(i with { End = now, Reason = i.Reason + " ; clôture après interruption" });
    }
    public Settings LoadSettings() { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT data FROM settings WHERE id=1"; return cmd.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Settings>(json)! : new(); }
    public void SaveSettings(Settings settings) { settings.Validate(); using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "INSERT OR REPLACE INTO settings VALUES(1,$data)"; cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(settings)); cmd.ExecuteNonQuery(); }
    public void Purge(DateTimeOffset cutoff)
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        // Conserve toutes les données des sessions retenues et leurs baselines.
        cmd.CommandText = """
        DELETE FROM sessions WHERE finish IS NOT NULL AND finish < $cutoff;
        DELETE FROM probes WHERE at < $cutoff AND NOT EXISTS(SELECT 1 FROM sessions s WHERE probes.at >= s.baseline AND (s.finish IS NULL OR probes.at <= s.finish));
        DELETE FROM network_events WHERE at < $cutoff AND NOT EXISTS(SELECT 1 FROM sessions s WHERE network_events.at >= s.baseline AND (s.finish IS NULL OR network_events.at <= s.finish));
        DELETE FROM incidents WHERE start < $cutoff AND NOT EXISTS(SELECT 1 FROM sessions s WHERE incidents.start >= s.baseline);
        """;
        cmd.Parameters.AddWithValue("$cutoff", Stamp(cutoff)); cmd.ExecuteNonQuery();
    }
    public bool HasBaseline(Session session)
    { using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT EXISTS(SELECT 1 FROM probes WHERE at >= $start AND at < $end)";cmd.Parameters.AddWithValue("$start",Stamp(session.BaselineStart));cmd.Parameters.AddWithValue("$end",Stamp(session.Start));return (long)cmd.ExecuteScalar()! != 0; }
    public void BackupArchive(string destination)
    {
        string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{using(var source=Open())using(var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(temp,"monitor.db"),Pooling=false}.ToString())){backup.Open();source.BackupDatabase(backup);}
        File.WriteAllText(Path.Combine(temp,"settings.json"),JsonSerializer.Serialize(LoadSettings(),new JsonSerializerOptions{WriteIndented=true}));
        foreach(var name in new[]{"reports","logs"}){var source=Path.Combine(Path.GetDirectoryName(DatabasePath)!,name);if(!Directory.Exists(source))continue;foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){var dest=Path.Combine(temp,name,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(file,dest);}}
        System.IO.Compression.ZipFile.CreateFromDirectory(temp,destination);
        }finally{SqliteConnection.ClearAllPools();Directory.Delete(temp,true);}
    }
    public void DeleteAll() { using var c = Open(); using var tx = c.BeginTransaction(); using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "PRAGMA secure_delete=ON; DELETE FROM probes; DELETE FROM sessions; DELETE FROM incidents; DELETE FROM network_events; DELETE FROM settings;"; cmd.ExecuteNonQuery(); tx.Commit(); }
}
