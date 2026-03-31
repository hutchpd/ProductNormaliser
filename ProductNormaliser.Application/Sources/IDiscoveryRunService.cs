using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface IDiscoveryRunService
{
    Task<DiscoveryRun> CreateAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default);

    Task<DiscoveryRun> CreateScheduledAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default);

    Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default);

    Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidatePage> QueryCandidatesAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default);

    Task<DiscoveryRun?> PauseAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRun?> ResumeAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRun?> StopAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidate?> AcceptCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidate?> DismissCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidate?> RestoreCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default);
}