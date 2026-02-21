using System.Collections.Generic;

public class FileIndex
{
    private struct Entry
    {
        public string LowerName;
        public string Path;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private const int MaxResults = 100;

    public void Clear()
    {
        entries.Clear();
    }

    public void Add(string lowerName, string path)
    {
        entries.Add(new Entry { LowerName = lowerName, Path = path });
    }

    public List<string> Search(string query)
    {
        if (string.IsNullOrEmpty(query))
            return new List<string>();

        var scored = new List<(string path, int score)>();

        for (int i = 0; i < entries.Count; i++)
        {
            int score = Score(entries[i].LowerName, query);
            if (score > 0)
                scored.Add((entries[i].Path, score));
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));

        int count = scored.Count < MaxResults ? scored.Count : MaxResults;
        var results = new List<string>(count);
        for (int i = 0; i < count; i++)
            results.Add(scored[i].path);

        return results;
    }

    private static int Score(string name, string query)
    {
        // Exact match
        if (name == query)
            return 10000;

        // Prefix match
        if (name.Length >= query.Length && name.StartsWith(query))
            return 5000 - name.Length;

        // Contains match
        if (name.Contains(query))
            return 2500 - name.Length;

        // Subsequence match — all query chars must appear in order
        int score = 0;
        int nameIndex = 0;
        int consecutive = 0;

        for (int qi = 0; qi < query.Length; qi++)
        {
            char c = query[qi];
            int foundAt = name.IndexOf(c, nameIndex);

            if (foundAt < 0)
                return 0; // Not a subsequence

            // Bonus for consecutive characters
            if (foundAt == nameIndex)
            {
                consecutive++;
                score += 10 + consecutive * 5;
            }
            else
            {
                consecutive = 0;
                score += 5;
            }

            // Bonus for matching at word boundaries (after _ or -)
            if (foundAt == 0 || name[foundAt - 1] == '_' || name[foundAt - 1] == '-' || name[foundAt - 1] == '.')
                score += 20;

            nameIndex = foundAt + 1;
        }

        // Penalize longer names (prefer tighter matches)
        score -= name.Length;

        return score > 0 ? score : 1;
    }
}
