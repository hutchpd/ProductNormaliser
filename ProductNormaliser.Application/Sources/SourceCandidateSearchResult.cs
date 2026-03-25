namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateSearchResult
{
    public string CandidateKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string CandidateType { get; init; } = string.Empty;
    public IReadOnlyList<string> MatchedCategoryKeys { get; init; } = [];
    public IReadOnlyList<string> MatchedBrandHints { get; init; } = [];
    public IReadOnlyList<string> SearchReasons { get; init; } = [];
}