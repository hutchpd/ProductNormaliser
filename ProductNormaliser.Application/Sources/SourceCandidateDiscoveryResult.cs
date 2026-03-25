namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateDiscoveryResult
{
    public IReadOnlyList<string> RequestedCategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<SourceCandidateResult> Candidates { get; init; } = [];
}