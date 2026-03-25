namespace ProductNormaliser.Application.Sources;

public sealed class DiscoverSourceCandidatesRequest
{
    public IReadOnlyCollection<string> CategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public IReadOnlyCollection<string> BrandHints { get; init; } = [];
    public int MaxCandidates { get; init; } = 10;
}