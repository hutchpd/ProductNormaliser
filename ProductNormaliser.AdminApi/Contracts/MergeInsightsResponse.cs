namespace ProductNormaliser.AdminApi.Contracts;

public sealed class MergeInsightsResponse
{
    public string CategoryKey { get; init; } = default!;
    public IReadOnlyList<MergeConflictInsightDto> OpenConflicts { get; init; } = [];
    public IReadOnlyList<AttributeMappingSuggestionDto> AttributeSuggestions { get; init; } = [];
}