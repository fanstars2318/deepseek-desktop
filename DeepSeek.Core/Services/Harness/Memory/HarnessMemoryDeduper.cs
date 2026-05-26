namespace DeepSeekBrowser.Services.Harness.Memory;

/// <summary>写入前合并高相似度记忆（Mem0 式去重）。</summary>
public static class HarnessMemoryDeduper
{
    public const double MergeThreshold = 0.92;

    public static HarnessMemoryRecord PrepareInsert(
        HarnessSemanticMemoryStore store,
        HarnessMemoryRecord incoming,
        float[] embedding,
        int compareLimit = 40)
    {
        var existing = store.Search(embedding, [incoming.Scope], compareLimit);
        foreach (var hit in existing)
        {
            var score = CosineSimilarity(embedding, hit.Embedding);
            if (score < MergeThreshold) continue;

            var mergedText = MergeTexts(hit.Text, incoming.Text);
            return new HarnessMemoryRecord
            {
                Id = hit.Id,
                Scope = incoming.Scope,
                Text = mergedText,
                Embedding = embedding,
                MetadataJson = incoming.MetadataJson,
                ContentHash = HarnessEmbeddingClient.ComputeHash(mergedText + "|" + incoming.Scope),
                CreatedAtUnix = hit.CreatedAtUnix,
                UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        return new HarnessMemoryRecord
        {
            Id = incoming.Id,
            Scope = incoming.Scope,
            Text = incoming.Text,
            Embedding = embedding,
            MetadataJson = incoming.MetadataJson,
            ContentHash = incoming.ContentHash,
            CreatedAtUnix = incoming.CreatedAtUnix,
            UpdatedAtUnix = incoming.UpdatedAtUnix
        };
    }

    private static string MergeTexts(string a, string b)
    {
        a = a.Trim();
        b = b.Trim();
        if (a.Contains(b, StringComparison.Ordinal)) return a;
        if (b.Contains(a, StringComparison.Ordinal)) return b;
        return a + "\n---\n" + b;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
