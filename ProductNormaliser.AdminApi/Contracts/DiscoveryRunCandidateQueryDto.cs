namespace ProductNormaliser.AdminApi.Contracts;

public sealed class DiscoveryRunCandidateQueryDto
{
    public string? StateFilter { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}