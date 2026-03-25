namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateDiscoveryResponseDto
{
    public IReadOnlyList<string> RequestedCategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<SourceCandidateDto> Candidates { get; init; } = [];
}