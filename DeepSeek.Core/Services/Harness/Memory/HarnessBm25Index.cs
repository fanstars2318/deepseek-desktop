using System.Text.RegularExpressions;

namespace DeepSeekBrowser.Services.Harness.Memory;

public sealed class HarnessBm25Index
{
    private readonly IReadOnlyList<DocEntry> _docs;
    private readonly double _avgLen;
    private readonly Dictionary<string, double> _idf;

    private sealed record DocEntry(string Id, string Text, Dictionary<string, int> TermFreq, int Length);

    private HarnessBm25Index(IReadOnlyList<DocEntry> docs, double avgLen, Dictionary<string, double> idf)
    {
        _docs = docs;
        _avgLen = avgLen;
        _idf = idf;
    }

    public static HarnessBm25Index Build(IReadOnlyList<(string Id, string Text)> documents)
    {
        if (documents.Count == 0)
            return new HarnessBm25Index(Array.Empty<DocEntry>(), 0, new Dictionary<string, double>());

        var docs = documents
            .Select(d =>
            {
                var terms = Tokenize(d.Text);
                var tf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in terms)
                    tf[t] = tf.GetValueOrDefault(t) + 1;
                return new DocEntry(d.Id, d.Text, tf, terms.Count);
            })
            .ToList();

        var avgLen = docs.Average(d => (double)d.Length);
        var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            foreach (var term in doc.TermFreq.Keys)
                df[term] = df.GetValueOrDefault(term) + 1;
        }

        var n = docs.Count;
        var idf = df.ToDictionary(
            kv => kv.Key,
            kv => Math.Log(1 + (n - kv.Value + 0.5) / (kv.Value + 0.5)),
            StringComparer.OrdinalIgnoreCase);

        return new HarnessBm25Index(docs, avgLen, idf);
    }

    public IReadOnlyList<(string Id, double Score)> Rank(string query, int topK)
    {
        if (_docs.Count == 0 || topK <= 0)
            return Array.Empty<(string, double)>();

        var qTerms = Tokenize(query);
        if (qTerms.Count == 0)
            return Array.Empty<(string, double)>();

        const double k1 = 1.2;
        const double b = 0.75;
        var scored = new List<(string Id, double Score)>();
        foreach (var doc in _docs)
        {
            double score = 0;
            foreach (var term in qTerms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!doc.TermFreq.TryGetValue(term, out var tf)) continue;
                if (!_idf.TryGetValue(term, out var idf)) continue;
                var denom = tf + k1 * (1 - b + b * doc.Length / Math.Max(_avgLen, 1));
                score += idf * (tf * (k1 + 1)) / denom;
            }

            if (score > 0)
                scored.Add((doc.Id, score));
        }

        return scored.OrderByDescending(x => x.Score).Take(topK).ToList();
    }

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}_]+")
            .Select(m => m.Value)
            .Where(t => t.Length > 1)
            .ToList();
    }
}
