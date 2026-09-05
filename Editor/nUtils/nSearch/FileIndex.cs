using System;
using System.Collections.Generic;

/// <summary>
/// Flat fuzzy index over asset paths, tuned for large projects. Each entry carries
/// a bitmask of the characters its name uses, so a query rejects most of the project
/// with a single AND before any string work happens, and matches are gathered into a
/// bounded min-heap so a keystroke never sorts every asset you own.
/// </summary>
public class FileIndex
{
    private struct Entry
    {
        public string LowerName;
        public string Path;
        public uint CharMask;
    }

    private const int MaxResults = 100;

    private readonly List<Entry> entries = new List<Entry>();

    // Lets the asset postprocessor tell a genuinely new asset from a re-import of one
    // already indexed, so saving a script does not invalidate the whole index.
    private readonly HashSet<string> indexedPaths = new HashSet<string>(StringComparer.Ordinal);

    // Top-K scratch, reused between searches so typing allocates nothing here.
    private readonly int[] heapScores = new int[MaxResults];
    private readonly string[] heapPaths = new string[MaxResults];

    public int Count => entries.Count;

    public void Clear()
    {
        entries.Clear();
        indexedPaths.Clear();
    }

    public bool ContainsPath(string path)
    {
        return indexedPaths.Contains(path);
    }

    /// <summary>Pre-size the backing list so a large project doesn't regrow it repeatedly.</summary>
    public void Reserve(int capacity)
    {
        if (capacity > entries.Capacity)
            entries.Capacity = capacity;
    }

    public void Add(string lowerName, string path)
    {
        entries.Add(new Entry
        {
            LowerName = lowerName,
            Path = path,
            CharMask = BuildMask(lowerName),
        });

        indexedPaths.Add(path);
    }

    public List<string> Search(string query)
    {
        if (string.IsNullOrEmpty(query))
            return new List<string>();

        uint queryMask = BuildMask(query);
        int queryLength = query.Length;
        int heapCount = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];

            // A name can only contain the query as a subsequence if it uses every
            // character the query does. One AND rejects the vast majority of assets.
            if ((queryMask & ~entry.CharMask) != 0)
                continue;

            if (entry.LowerName.Length < queryLength)
                continue;

            int score = Score(entry.LowerName, query);
            if (score <= 0)
                continue;

            if (heapCount < MaxResults)
            {
                heapScores[heapCount] = score;
                heapPaths[heapCount] = entry.Path;
                SiftUp(heapCount);
                heapCount++;
            }
            else if (score > heapScores[0])
            {
                // Heap root is the weakest kept result; this one displaces it.
                heapScores[0] = score;
                heapPaths[0] = entry.Path;
                SiftDown(0, heapCount);
            }
        }

        var ordered = new List<(string path, int score)>(heapCount);
        for (int i = 0; i < heapCount; i++)
            ordered.Add((heapPaths[i], heapScores[i]));

        // Path breaks score ties so equally-ranked results don't reshuffle between keystrokes.
        ordered.Sort((a, b) =>
        {
            int byScore = b.score.CompareTo(a.score);
            return byScore != 0 ? byScore : string.CompareOrdinal(a.path, b.path);
        });

        var results = new List<string>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
            results.Add(ordered[i].path);

        return results;
    }

    /// <summary>
    /// One bit per a-z, plus six buckets covering the digits. Collisions only cost a
    /// wasted scoring pass, never a missed match.
    /// </summary>
    private static uint BuildMask(string value)
    {
        uint mask = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c >= 'a' && c <= 'z')
                mask |= 1u << (c - 'a');
            else if (c >= '0' && c <= '9')
                mask |= 1u << (26 + (c - '0') % 6);
        }

        return mask;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (heapScores[parent] <= heapScores[index])
                break;

            Swap(parent, index);
            index = parent;
        }
    }

    private void SiftDown(int index, int count)
    {
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= count)
                break;

            int smallest = left;
            int right = left + 1;
            if (right < count && heapScores[right] < heapScores[left])
                smallest = right;

            if (heapScores[index] <= heapScores[smallest])
                break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        int score = heapScores[a];
        heapScores[a] = heapScores[b];
        heapScores[b] = score;

        string path = heapPaths[a];
        heapPaths[a] = heapPaths[b];
        heapPaths[b] = path;
    }

    private static int Score(string name, string query)
    {
        // Exact match
        if (string.Equals(name, query, StringComparison.Ordinal))
            return 10000;

        // Prefix match
        if (name.StartsWith(query, StringComparison.Ordinal))
            return 5000 - name.Length;

        // Contains match
        if (name.IndexOf(query, StringComparison.Ordinal) >= 0)
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
