using System.Collections.Generic;

public interface ISearchCommand
{
    string Prefix { get; }
    string Description { get; }
    void GetResults(string query, List<SearchResult> results);
}
