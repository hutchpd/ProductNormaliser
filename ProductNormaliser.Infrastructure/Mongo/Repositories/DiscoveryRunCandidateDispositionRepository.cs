using MongoDB.Driver;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class DiscoveryRunCandidateDispositionRepository(MongoDbContext context)
    : MongoRepositoryBase<DiscoveryRunCandidateDisposition>(context.DiscoveryRunCandidateDispositions), IDiscoveryRunCandidateDispositionStore
{
    public async Task<IReadOnlyList<DiscoveryRunCandidateDisposition>> FindActiveMatchesAsync(
        string scopeFingerprint,
        string normalizedHost,
        string normalizedBaseUrl,
        string normalizedDisplayName,
        IReadOnlyCollection<string> allowedMarkets,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DiscoveryRunCandidateDisposition>.Filter.Eq(disposition => disposition.IsActive, true)
            & Builders<DiscoveryRunCandidateDisposition>.Filter.Eq(disposition => disposition.ScopeFingerprint, scopeFingerprint);

        var identityFilters = new List<FilterDefinition<DiscoveryRunCandidateDisposition>>();
        if (!string.IsNullOrWhiteSpace(normalizedHost))
        {
            identityFilters.Add(Builders<DiscoveryRunCandidateDisposition>.Filter.Eq(disposition => disposition.NormalizedHost, normalizedHost));
        }

        if (!string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            identityFilters.Add(Builders<DiscoveryRunCandidateDisposition>.Filter.Eq(disposition => disposition.NormalizedBaseUrl, normalizedBaseUrl));
        }

        if (!string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            identityFilters.Add(Builders<DiscoveryRunCandidateDisposition>.Filter.Eq(disposition => disposition.NormalizedDisplayName, normalizedDisplayName));
        }

        if (identityFilters.Count == 0)
        {
            return [];
        }

        filter &= Builders<DiscoveryRunCandidateDisposition>.Filter.Or(identityFilters);

        var dispositions = await Collection.Find(filter)
            .SortByDescending(disposition => disposition.UpdatedUtc)
            .ToListAsync(cancellationToken);

        var normalizedMarkets = DiscoveryRunCandidateIdentity.NormalizeMarkets(allowedMarkets);
        return dispositions
            .Where(disposition => string.Equals(disposition.NormalizedHost, normalizedHost, StringComparison.OrdinalIgnoreCase)
                || string.Equals(disposition.NormalizedBaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(disposition.NormalizedDisplayName, normalizedDisplayName, StringComparison.OrdinalIgnoreCase)
                    && DiscoveryRunCandidateIdentity.ShareAnyMarket(disposition.AllowedMarkets, normalizedMarkets)))
            .ToArray();
    }

    public async Task UpsertAsync(DiscoveryRunCandidateDisposition disposition, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(existing => existing.Id == disposition.Id, disposition, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }
}