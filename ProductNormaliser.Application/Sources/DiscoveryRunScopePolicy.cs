using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

internal static class DiscoveryRunScopePolicy
{
    public static string CreateFingerprint(DiscoveryRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return CreateFingerprint(run.Market, run.Locale, run.RequestedCategoryKeys);
    }

    public static string CreateFingerprint(string? market, string? locale, IReadOnlyCollection<string> categoryKeys)
    {
        var normalizedMarket = NormalizeOptional(market);
        var normalizedLocale = NormalizeOptional(locale);
        var normalizedCategories = categoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        return $"market:{normalizedMarket}|locale:{normalizedLocale}|categories:{string.Join(',', normalizedCategories)}";
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}