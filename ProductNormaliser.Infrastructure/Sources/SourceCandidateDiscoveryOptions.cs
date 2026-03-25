namespace ProductNormaliser.Infrastructure.Sources;

public sealed class SourceCandidateDiscoveryOptions
{
    public const string SectionName = "SourceCandidateDiscovery";

    public int SearchTimeoutSeconds { get; init; } = 15;

    public int ProbeTimeoutSeconds { get; init; } = 10;

    public int MaxSearchQueries { get; init; } = 5;
}