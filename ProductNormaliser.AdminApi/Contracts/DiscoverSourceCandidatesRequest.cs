namespace ProductNormaliser.AdminApi.Contracts;

public sealed class DiscoverSourceCandidatesRequest
{
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public int MaxCandidates { get; init; } = 10;
}