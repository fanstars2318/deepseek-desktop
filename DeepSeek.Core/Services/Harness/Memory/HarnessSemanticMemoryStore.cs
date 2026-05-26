using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DeepSeekBrowser.Services.Harness.Memory;

public sealed class HarnessSemanticMemoryStore : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _conn;

    public HarnessSemanticMemoryStore(string? dbPathOverride = null)
    {
        _dbPath = dbPathOverride ?? Path.Combine(
            AgentDesktopConfigSync.HomeDirectory, "memory", "semantic.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _conn = new SqliteConnection("Data Source=" + _dbPath);
        _conn.Open();
        EnsureSchema();
    }

    public static string DefaultDbPath =>
        Path.Combine(AgentDesktopConfigSync.HomeDirectory, "memory", "semantic.db");

    public void AddOrUpdate(HarnessMemoryRecord record)
    {
        const string sql = """
            INSERT INTO memories (id, scope, text, embedding, metadata, content_hash, created_at, updated_at)
            VALUES ($id, $scope, $text, $embedding, $metadata, $hash, $created, $updated)
            ON CONFLICT(content_hash, scope) DO UPDATE SET
              text = excluded.text,
              embedding = excluded.embedding,
              metadata = excluded.metadata,
              updated_at = excluded.updated_at;
            """;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", record.Id);
        cmd.Parameters.AddWithValue("$scope", record.Scope);
        cmd.Parameters.AddWithValue("$text", record.Text);
        cmd.Parameters.AddWithValue("$embedding", SerializeEmbedding(record.Embedding));
        cmd.Parameters.AddWithValue("$metadata", (object?)record.MetadataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hash", record.ContentHash);
        cmd.Parameters.AddWithValue("$created", record.CreatedAtUnix);
        cmd.Parameters.AddWithValue("$updated", record.UpdatedAtUnix);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<HarnessMemoryRecord> Search(
        float[] queryEmbedding,
        IReadOnlyList<string> scopes,
        int topK)
    {
        if (queryEmbedding.Length == 0 || topK <= 0)
            return Array.Empty<HarnessMemoryRecord>();

        var scopeFilter = scopes.Count == 0
            ? ""
            : " WHERE scope IN (" + string.Join(",", scopes.Select((_, i) => "$s" + i)) + ")";

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, scope, text, embedding, metadata, content_hash, created_at, updated_at FROM memories" + scopeFilter;
        for (var i = 0; i < scopes.Count; i++)
            cmd.Parameters.AddWithValue("$s" + i, scopes[i]);

        var scored = new List<(HarnessMemoryRecord Record, double Score)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var emb = DeserializeEmbedding(reader.GetString(3));
            if (emb.Length == 0) continue;
            var score = CosineSimilarity(queryEmbedding, emb);
            scored.Add((ReadRecord(reader), score));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Record)
            .ToList();
    }

    public IReadOnlyList<HarnessMemorySearchHit> SearchByTextPrefix(string query, int limit = 20)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, scope, text, metadata, updated_at
            FROM memories
            WHERE text LIKE $q
            ORDER BY updated_at DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$q", "%" + query + "%");
        cmd.Parameters.AddWithValue("$limit", limit);

        var hits = new List<HarnessMemorySearchHit>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            hits.Add(new HarnessMemorySearchHit
            {
                Id = reader.GetString(0),
                Scope = reader.GetString(1),
                Text = reader.GetString(2),
                MetadataJson = reader.IsDBNull(3) ? null : reader.GetString(3),
                UpdatedAtUnix = reader.GetInt64(4)
            });
        }

        return hits;
    }

    public bool Forget(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM memories WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public int Count()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM memories;";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int PruneScopesOlderThan(IReadOnlyList<string> scopePrefixes, long minUpdatedUnix)
    {
        if (scopePrefixes.Count == 0) return 0;
        var clauses = scopePrefixes.Select((_, i) => "scope LIKE $p" + i).ToList();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM memories WHERE ({string.Join(" OR ", clauses)}) AND updated_at < $cutoff;";
        for (var i = 0; i < scopePrefixes.Count; i++)
            cmd.Parameters.AddWithValue("$p" + i, scopePrefixes[i] + "%");
        cmd.Parameters.AddWithValue("$cutoff", minUpdatedUnix);
        return cmd.ExecuteNonQuery();
    }

    public int ClearScopePrefix(string scopePrefix)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM memories WHERE scope LIKE $p;";
        cmd.Parameters.AddWithValue("$p", scopePrefix + "%");
        return cmd.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS memories (
              id TEXT PRIMARY KEY,
              scope TEXT NOT NULL,
              text TEXT NOT NULL,
              embedding TEXT NOT NULL,
              metadata TEXT,
              content_hash TEXT NOT NULL,
              created_at INTEGER NOT NULL,
              updated_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_memories_scope ON memories(scope);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_memories_hash_scope ON memories(content_hash, scope);
            """;
        cmd.ExecuteNonQuery();
    }

    private static HarnessMemoryRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Scope = reader.GetString(1),
        Text = reader.GetString(2),
        Embedding = DeserializeEmbedding(reader.GetString(3)),
        MetadataJson = reader.IsDBNull(4) ? null : reader.GetString(4),
        ContentHash = reader.GetString(5),
        CreatedAtUnix = reader.GetInt64(6),
        UpdatedAtUnix = reader.GetInt64(7)
    };

    private static string SerializeEmbedding(float[] vector) =>
        string.Join(',', vector.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    private static float[] DeserializeEmbedding(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<float>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => float.Parse(s, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na <= 0 || nb <= 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    public void Dispose() => _conn.Dispose();
}

public sealed class HarnessMemorySearchHit
{
    public string Id { get; init; } = "";
    public string Scope { get; init; } = "";
    public string Text { get; init; } = "";
    public string? MetadataJson { get; init; }
    public long UpdatedAtUnix { get; init; }
}
