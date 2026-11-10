using RayLite.Models;

namespace RayLite.Services;

public sealed class SearchEngine
{
    private volatile IndexEntry[] _entries = Array.Empty<IndexEntry>();

    public int Count => _entries.Length;

    /// <summary>Atomically swaps the index; searches in flight keep using the old array.</summary>
    public void SetIndex(IndexEntry[] entries) => _entries = entries;

    public List<IndexEntry> Search(string query, int maxResults)
    {
        string q = query.Trim().ToLowerInvariant();
        var top = new List<(int Score, IndexEntry Entry)>(maxResults + 1);
        if (q.Length == 0) return new List<IndexEntry>();

        string compact = q.Contains(' ') ? q.Replace(" ", string.Empty) : q;
        IndexEntry[] entries = _entries;

        foreach (IndexEntry entry in entries)
        {
            bool isApp = entry.Kind is EntryKind.Application or EntryKind.UwpApp;
            int score = FuzzyMatcher.Score(q, compact, entry.Name, isApp);
            if (score <= 0) continue;

            score += entry.Kind switch
            {
                EntryKind.Application => 60,
                EntryKind.UwpApp => 60,
                EntryKind.Folder => 10,
                _ => 0
            };

            if (top.Count == maxResults && score <= top[^1].Score) continue;

            int i = top.Count;
            while (i > 0 && top[i - 1].Score < score) i--;
            top.Insert(i, (score, entry));
            if (top.Count > maxResults) top.RemoveAt(top.Count - 1);
        }

        var result = new List<IndexEntry>(top.Count);
        foreach (var item in top) result.Add(item.Entry);
        return result;
    }
}

/// <summary>
/// Scoring tiers: exact (1000) > prefix (800) > substring (300-650) > subsequence (1-299) > typo-tolerant (≤150, apps only).
/// </summary>
internal static class FuzzyMatcher
{
    public static int Score(string q, string compact, ReadOnlySpan<char> text, bool allowTypos)
    {
        if (text.Length == 0 || q.Length == 0) return 0;

        // 1. Exact / prefix / substring
        int idx = text.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            int lengthPenalty = Math.Min(text.Length - q.Length, 100);
            if (idx == 0) return (text.Length == q.Length ? 1000 : 800) - lengthPenalty;
            int boundaryBonus = IsBoundary(text, idx) ? 150 : 0;
            return 500 + boundaryBonus - Math.Min(idx, 100) - lengthPenalty;
        }

        // 2. Subsequence ("vsc" -> "Visual Studio Code")
        if (compact.Length <= text.Length)
        {
            int qi = 0, run = 0, score = 0, first = -1, last = -1;
            for (int ti = 0; ti < text.Length && qi < compact.Length; ti++)
            {
                if (char.ToLowerInvariant(text[ti]) == compact[qi])
                {
                    if (first < 0) first = ti;
                    last = ti;
                    score += 10 + run * 8 + (IsBoundary(text, ti) ? 25 : 0);
                    run++;
                    qi++;
                }
                else
                {
                    run = 0;
                }
            }

            if (qi == compact.Length && last - first + 1 <= compact.Length * 3 + 2)
                return Math.Clamp(score - (text.Length - compact.Length), 1, 299);
        }

        // 3. Typo tolerance (transpositions, substitutions, single insert/delete) for app names
        if (allowTypos && compact.Length is >= 4 and <= 24 &&
            text.Length >= compact.Length - 1 &&
            char.ToLowerInvariant(text[0]) == compact[0])
        {
            int maxDistance = compact.Length >= 7 ? 2 : 1;
            int best = int.MaxValue;
            for (int len = compact.Length - 1; len <= compact.Length + 1; len++)
            {
                if (len <= 0 || len > text.Length) continue;
                best = Math.Min(best, DamerauDistance(compact, text[..len]));
            }
            if (best <= maxDistance) return 150 - best * 40;
        }

        return 0;
    }

    private static bool IsBoundary(ReadOnlySpan<char> text, int i)
    {
        if (i == 0) return true;
        char prev = text[i - 1];
        return !char.IsLetterOrDigit(prev) || (char.IsLower(prev) && char.IsUpper(text[i]));
    }

    /// <summary>Optimal-string-alignment distance. <paramref name="a"/> must already be lower-case.</summary>
    private static int DamerauDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int n = a.Length, m = b.Length, w = m + 1;
        Span<int> d = stackalloc int[(n + 1) * w];

        for (int i = 0; i <= n; i++) d[i * w] = i;
        for (int j = 0; j <= m; j++) d[j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                char bj = char.ToLowerInvariant(b[j - 1]);
                int cost = a[i - 1] == bj ? 0 : 1;
                int v = Math.Min(Math.Min(d[(i - 1) * w + j] + 1, d[i * w + j - 1] + 1), d[(i - 1) * w + j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == char.ToLowerInvariant(b[j - 2]) && a[i - 2] == bj)
                    v = Math.Min(v, d[(i - 2) * w + j - 2] + 1);

                d[i * w + j] = v;
            }
        }
        return d[n * w + m];
    }
}
