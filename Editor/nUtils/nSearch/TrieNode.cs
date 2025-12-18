using System.Collections.Generic;
using System.Linq;

public class TrieNode
{
    private Dictionary<char, TrieNode> children = new Dictionary<char, TrieNode>();
    private List<string> files = new List<string>();
    private const int MaxResults = 100;

    public void Insert(string key, string filePath)
    {
        TrieNode current = this;
        foreach (char c in key)
        {
            if (!current.children.ContainsKey(c))
            {
                current.children[c] = new TrieNode();
            }
            current = current.children[c];
        }

        // Avoid duplicates
        if (!current.files.Contains(filePath))
        {
            current.files.Add(filePath);
        }
    }

    public List<string> Search(string query)
    {
        var results = new HashSet<string>();

        if (string.IsNullOrEmpty(query))
            return new List<string>();

        // Direct prefix match
        CollectPrefixMatches(query, results);

        // Fuzzy matches if we need more results
        if (results.Count < MaxResults)
        {
            CollectFuzzyMatches(query, results);
        }

        return results.Take(MaxResults).ToList();
    }

    private void CollectPrefixMatches(string query, HashSet<string> results)
    {
        TrieNode current = this;

        foreach (char c in query)
        {
            if (!current.children.ContainsKey(c))
                return;
            current = current.children[c];
        }

        // Collect all files under this node
        CollectAllFiles(current, results);
    }

    private void CollectFuzzyMatches(string query, HashSet<string> results)
    {
        if (results.Count >= MaxResults) return;

        // Only do fuzzy matching for queries 3+ chars
        if (query.Length < 3) return;

        SearchFuzzy(this, query, 0, results, 0);
    }

    private void SearchFuzzy(TrieNode node, string query, int queryIndex, HashSet<string> results, int depth)
    {
        if (results.Count >= MaxResults || depth > 20) return;

        if (queryIndex >= query.Length)
        {
            CollectAllFiles(node, results);
            return;
        }

        char targetChar = query[queryIndex];

        // Exact match - prioritize this
        if (node.children.ContainsKey(targetChar))
        {
            SearchFuzzy(node.children[targetChar], query, queryIndex + 1, results, depth);
        }

        // Skip this character in query (allows for gaps)
        if (queryIndex < query.Length - 1)
        {
            foreach (var child in node.children)
            {
                SearchFuzzy(child.Value, query, queryIndex + 1, results, depth + 1);
            }
        }
    }

    private void CollectAllFiles(TrieNode node, HashSet<string> results)
    {
        if (results.Count >= MaxResults) return;

        foreach (string file in node.files)
        {
            results.Add(file);
            if (results.Count >= MaxResults) return;
        }

        foreach (var child in node.children.Values)
        {
            CollectAllFiles(child, results);
            if (results.Count >= MaxResults) return;
        }
    }
}