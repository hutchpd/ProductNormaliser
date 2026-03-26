namespace ProductNormaliser.Application.Sources;

public interface ISourceCandidateProbeService
{
    Task<SourceCandidateProbeResult> ProbeAsync(
        SourceCandidateSearchResult candidate,
        IReadOnlyCollection<string> categoryKeys,
        string automationMode,
        CancellationToken cancellationToken = default);
}