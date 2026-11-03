namespace Wormy.Core;

public static class FileSearch
{
    public static bool Matches(SearchQuery query, FileEntry file, IReadOnlyList<string> tags, bool indexedContentMatch, bool relatedWords = false) =>
        query.MatchesMetadata(file) && (indexedContentMatch || query.Terms.All(term =>
            (relatedWords ? RelatedSearch.Alternatives(term) : new[] { term }).Any(candidate => file.Name.Contains(candidate, StringComparison.OrdinalIgnoreCase) || (System.IO.Path.GetDirectoryName(file.Path) ?? "").Contains(candidate,StringComparison.OrdinalIgnoreCase) || (file.DownloadSource ?? "").Contains(candidate,StringComparison.OrdinalIgnoreCase) || tags.Any(tag => tag.Contains(candidate, StringComparison.OrdinalIgnoreCase)))));
}
