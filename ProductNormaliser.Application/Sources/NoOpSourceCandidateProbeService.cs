namespace ProductNormaliser.Application.Sources;

public sealed class NoOpSourceCandidateProbeService : ISourceCandidateProbeService
{
    public Task<SourceCandidateProbeResult> ProbeAsync(SourceCandidateSearchResult candidate, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
    {
        _ = candidate;
        _ = categoryKeys;
        _ = cancellationToken;

        // TODO: integrate robots, sitemap, and category-relevance probing for candidate hosts.
        return Task.FromResult(new SourceCandidateProbeResult());
    }
}