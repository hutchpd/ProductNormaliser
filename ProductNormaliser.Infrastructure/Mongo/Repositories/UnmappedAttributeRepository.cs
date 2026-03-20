using MongoDB.Driver;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class UnmappedAttributeRepository(MongoDbContext context)
    : MongoRepositoryBase<UnmappedAttribute>(context.UnmappedAttributes), IUnmappedAttributeStore, IUnmappedAttributeRecorder
{
    private const int MaxSampleValues = 5;
    private const int MaxObservations = 10;

    public Task<IReadOnlyList<UnmappedAttribute>> ListAsync(string? categoryKey = null, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(categoryKey)
            ? Builders<UnmappedAttribute>.Filter.Empty
            : Builders<UnmappedAttribute>.Filter.Eq(attribute => attribute.CategoryKey, categoryKey);

        return base.ListAsync(filter, cancellationToken);
    }

    public void Record(string categoryKey, string canonicalKey, SourceAttributeValue rawAttribute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalKey);
        ArgumentNullException.ThrowIfNull(rawAttribute);

        var now = DateTime.UtcNow;
        var rawAttributeKey = rawAttribute.AttributeKey.Trim();
        var recordId = BuildId(categoryKey, canonicalKey, rawAttributeKey);
        var (sourceName, sourcePath) = ParseSourcePath(rawAttribute.SourcePath);
        var existing = Collection.Find(attribute => attribute.Id == recordId).FirstOrDefault();

        if (existing is null)
        {
            Collection.InsertOne(new UnmappedAttribute
            {
                Id = recordId,
                CategoryKey = categoryKey,
                CanonicalKey = canonicalKey,
                RawAttributeKey = rawAttributeKey,
                OccurrenceCount = 1,
                SourceNames = CreateDistinctList(sourceName),
                SampleValues = CreateDistinctList(rawAttribute.Value),
                FirstSeenUtc = now,
                LastSeenUtc = now,
                RecentObservations =
                [
                    new UnmappedAttributeObservation
                    {
                        SourceName = sourceName,
                        SourcePath = sourcePath,
                        RawValue = rawAttribute.Value,
                        ObservedUtc = now
                    }
                ]
            });

            return;
        }

        existing.OccurrenceCount += 1;
        existing.LastSeenUtc = now;
        AddDistinct(existing.SourceNames, sourceName, MaxSampleValues);
        AddDistinct(existing.SampleValues, rawAttribute.Value, MaxSampleValues);
        existing.RecentObservations.Add(new UnmappedAttributeObservation
        {
            SourceName = sourceName,
            SourcePath = sourcePath,
            RawValue = rawAttribute.Value,
            ObservedUtc = now
        });

        if (existing.RecentObservations.Count > MaxObservations)
        {
            existing.RecentObservations = existing.RecentObservations
                .OrderByDescending(observation => observation.ObservedUtc)
                .Take(MaxObservations)
                .ToList();
        }

        Collection.ReplaceOne(attribute => attribute.Id == recordId, existing, new ReplaceOptions { IsUpsert = true });
    }

    private static string BuildId(string categoryKey, string canonicalKey, string rawAttributeKey)
    {
        return string.Join(
            ":",
            categoryKey.Trim().ToLowerInvariant(),
            canonicalKey.Trim().ToLowerInvariant(),
            rawAttributeKey.Trim().ToLowerInvariant().Replace(' ', '-'));
    }

    private static (string SourceName, string? SourcePath) ParseSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !sourcePath.StartsWith("source:", StringComparison.OrdinalIgnoreCase))
        {
            return ("unknown-source", sourcePath);
        }

        var separatorIndex = sourcePath.IndexOf('|');
        if (separatorIndex < 0)
        {
            return (sourcePath[7..], null);
        }

        var sourceName = sourcePath[7..separatorIndex];
        var actualPath = separatorIndex + 1 < sourcePath.Length
            ? sourcePath[(separatorIndex + 1)..]
            : null;

        return (string.IsNullOrWhiteSpace(sourceName) ? "unknown-source" : sourceName, actualPath);
    }

    private static List<string> CreateDistinctList(string? value)
    {
        var items = new List<string>();
        AddDistinct(items, value, MaxSampleValues);
        return items;
    }

    private static void AddDistinct(List<string> values, string? candidate, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        if (values.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (values.Count >= maxCount)
        {
            return;
        }

        values.Add(candidate);
    }
}