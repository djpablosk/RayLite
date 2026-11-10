using RayLite.Models;

namespace RayLite.Services;

public sealed class SearchEngine
{
    private volatile IndexEntry[] _entries = Array.Empty<IndexEntry>();

    public int Count => _entries.Length;

    /// <summary>Atomically swaps the index; searches in flight keep using the old array.</summary>
    public void SetIndex(IndexEntry[] entries) => _entries = entries;

    /// <summary>One scored match. Ranking is the composite key (IsApp, Score, NameLength, PathLength).</summary>
    private readonly record struct Candidate(bool IsApp, int Score, int NameLength, int PathLength, IndexEntry Entry);

    /// <summary>
    /// Returns up to <paramref name="maxResults"/> matches ordered by:
    /// 1) applications before everything else, 2) match score (exact &gt; prefix &gt; substring &gt; subsequence/typo),
    /// 3) shorter name, 4) shorter path. Files and folders only fill slots left over after all matching apps.
    /// </summary>
    public List<IndexEntry> Search(string query, int maxResults)
    {
        string q = query.Trim().ToLowerInvariant();
        if (q.Length == 0 || maxResults <= 0) return new List<IndexEntry>();

        string compact = q.Contains(' ') ? q.Replace(" ", string.Empty) : q;
        IndexEntry[] entries = _entries;
        var top = new List<Candidate>(maxResults + 1);

        foreach (IndexEntry entry in entries)
        {
            bool isApp = entry.Kind is EntryKind.Application or EntryKind.UwpApp;

            // Once the list is full of apps, no file/folder can ever displace one: skip scoring them entirely.
            if (!isApp && top.Count == maxResults && top[^1].IsApp) continue;

            int score = FuzzyMatcher.Score(q, compact, entry.Name, isApp);
            if (score <= 0) continue;

            var candidate = new Candidate(isApp, score, entry.Name.Length, entry.Path.Length, entry);

            if (top.Count == maxResults && !IsBetter(candidate, top[^1])) continue;

            int i = top.Count;
            while (i > 0 && IsBetter(candidate, top[i - 1])) i--;
            top.Insert(i, candidate);
            if (top.Count > maxResults) top.RemoveAt(top.Count - 1);
        }

        var result = new List<IndexEntry>(top.Count);
        foreach (Candidate c in top) result.Add(c.Entry);
        return result;
    }

    /// <summary>Strict ordering; ties keep index order (earlier entries win).</summary>
    private static bool IsBetter(in Candidate a, in Candidate b)
    {
        if (a.IsApp != b.IsApp) return a.IsApp;
        if (a.Score != b.Score) return a.Score > b.Score;
        if (a.NameLength != b.NameLength) return a.NameLength < b.NameLength;
        return a.PathLength < b.PathLength;
    }
}

/// <summary>
/// Match-quality tiers (length is deliberately NOT part of the score; it is a separate ranking key):
/// exact 1000 &gt; prefix 800 &gt; substring 400–650 &gt; subsequence 1–299 / typo-tolerant (apps only) ≤150.
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
            if (idx == 0) return text.Length == q.Length ? 1000 : 800;
            int boundaryBonus = IsBoundary(text, idx) ? 150 : 0;
            return 500 + boundaryBonus - Math.Min(idx, 100);
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
                return Math.Clamp(score, 1, 299);
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
