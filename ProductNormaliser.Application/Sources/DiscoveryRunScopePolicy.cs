using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

internal static class DiscoveryRunScopePolicy
{
    public static string CreateFingerprint(DiscoveryRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return CreateFingerprint(run.Market, run.Locale, run.RequestedCategoryKeys, run.BrandHints);
    }

    public static string CreateFingerprint(string? market, string? locale, IReadOnlyCollection<string> categoryKeys, IReadOnlyCollection<string>? brandHints = null)
    {
        var normalizedMarket = NormalizeOptional(market);
        var normalizedLocale = NormalizeOptional(locale);
        var normalizedCategories = categoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        var normalizedBrands = (brandHints ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        return $"market:{normalizedMarket}|locale:{normalizedLocale}|categories:{string.Join(',', normalizedCategories)}|brands:{string.Join(',', normalizedBrands)}";
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}