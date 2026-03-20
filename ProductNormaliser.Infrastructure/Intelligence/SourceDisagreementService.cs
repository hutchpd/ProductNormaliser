using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Intelligence;

public sealed class SourceDisagreementService(ISourceAttributeDisagreementStore disagreementStore) : ISourceDisagreementService
{
    public decimal GetSourceAttributeAdjustment(string sourceName, string categoryKey, string attributeKey)
    {
        var disagreement = disagreementStore.GetAsync(sourceName, categoryKey, attributeKey).GetAwaiter().GetResult();
        if (disagreement is null || disagreement.TotalComparisons == 0)
        {
            return 1.00m;
        }

        var adjustment = 1.00m - disagreement.DisagreementRate * 0.50m + disagreement.WinRate * 0.25m;
        return Math.Min(1.20m, Math.Max(0.45m, decimal.Round(adjustment, 4, MidpointRounding.AwayFromZero)));
    }

    public IReadOnlyList<SourceAttributeDisagreement> GetDisagreements(string categoryKey, string? sourceName = null)
    {
        return disagreementStore.ListAsync(categoryKey, sourceName).GetAwaiter().GetResult();
    }

    public void RefreshForProduct(CanonicalProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);

        foreach (var attribute in product.Attributes.Values)
        {
            var groupedEvidence = attribute.Evidence
                .GroupBy(evidence => evidence.SourceName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(evidence => evidence.ObservedUtc).First())
                .ToArray();

            if (groupedEvidence.Length == 0)
            {
                continue;
            }

            foreach (var evidence in groupedEvidence)
            {
                var disagreement = disagreementStore.GetAsync(evidence.SourceName, product.CategoryKey, attribute.AttributeKey).GetAwaiter().GetResult()
                    ?? new SourceAttributeDisagreement
                    {
                        Id = $"disagreement:{evidence.SourceName}:{product.CategoryKey}:{attribute.AttributeKey}",
                        SourceName = evidence.SourceName,
                        CategoryKey = product.CategoryKey,
                        AttributeKey = attribute.AttributeKey
                    };

                disagreement.TotalComparisons += 1;

                var won = string.Equals(attribute.WinningSourceName, evidence.SourceName, StringComparison.OrdinalIgnoreCase);
                var agrees = attribute.Value is null
                    ? string.IsNullOrWhiteSpace(evidence.RawValue)
                    : string.Equals(attribute.Value.ToString()?.Trim(), evidence.RawValue?.Trim(), StringComparison.OrdinalIgnoreCase);

                if (!agrees)
                {
                    disagreement.TimesDisagreed += 1;
                }

                if (won)
                {
                    disagreement.TimesWon += 1;
                }

                disagreement.DisagreementRate = decimal.Round((decimal)disagreement.TimesDisagreed / disagreement.TotalComparisons, 4, MidpointRounding.AwayFromZero);
                disagreement.WinRate = decimal.Round((decimal)disagreement.TimesWon / disagreement.TotalComparisons, 4, MidpointRounding.AwayFromZero);
                disagreement.LastUpdatedUtc = product.UpdatedUtc == default ? DateTime.UtcNow : product.UpdatedUtc;

                disagreementStore.UpsertAsync(disagreement).GetAwaiter().GetResult();
            }
        }
    }
}