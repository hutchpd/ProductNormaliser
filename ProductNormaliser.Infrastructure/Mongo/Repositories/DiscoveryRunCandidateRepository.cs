using MongoDB.Driver;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class DiscoveryRunCandidateRepository(MongoDbContext context)
    : MongoRepositoryBase<DiscoveryRunCandidate>(context.DiscoveryRunCandidates), IDiscoveryRunCandidateStore
{
    public async Task<IReadOnlyList<DiscoveryRunCandidate>> ListByRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(candidate => candidate.RunId == runId)
            .SortByDescending(candidate => candidate.ConfidenceScore)
            .ThenBy(candidate => candidate.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<DiscoveryRunCandidate?> GetAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(candidate => candidate.RunId == runId && candidate.CandidateKey == candidateKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(DiscoveryRunCandidate candidate, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.RunId == candidate.RunId && existing.CandidateKey == candidate.CandidateKey,
            candidate,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> TryUpdateAsync(DiscoveryRunCandidate candidate, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var result = await Collection.ReplaceOneAsync(
            existing => existing.RunId == candidate.RunId
                && existing.CandidateKey == candidate.CandidateKey
                && existing.Revision == expectedRevision,
            candidate,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

        return result.ModifiedCount == 1;
    }
}