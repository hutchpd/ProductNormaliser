namespace ProductNormaliser.Infrastructure.Sources;

public sealed class SourceCandidateDiscoveryOptions
{
    public const string SectionName = "SourceCandidateDiscovery";

    public string SearchApiBaseUrl { get; init; } = "https://api.search.brave.com";

    public string? SearchApiKey { get; init; }

    public int SearchTimeoutSeconds { get; init; } = 15;

    public int ProbeTimeoutSeconds { get; init; } = 10;

    public int SearchRetryCount { get; init; } = 1;

    public int SearchRetryBaseDelayMs { get; init; } = 500;

    public int ProbeRetryCount { get; init; } = 1;

    public int ProbeRetryBaseDelayMs { get; init; } = 400;

    public int MaxSearchQueries { get; init; } = 5;
}