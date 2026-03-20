namespace ProductNormaliser.AdminApi.Contracts;

public sealed class AttributeMappingSuggestionDto
{
    public string RawAttributeKey { get; init; } = default!;
    public string SuggestedCanonicalKey { get; init; } = default!;
    public decimal Confidence { get; init; }
    public int OccurrenceCount { get; init; }
    public IReadOnlyList<string> SourceNames { get; init; } = [];
}