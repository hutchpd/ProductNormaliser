using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class InteractiveCategoryFilter
{
    public static IReadOnlyList<CategoryMetadataDto> Apply(IEnumerable<CategoryMetadataDto> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        return categories
            .Where(category => category.IsEnabled)
            .Where(category => string.Equals(category.CrawlSupportStatus, "Supported", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category.CrawlSupportStatus, "Experimental", StringComparison.OrdinalIgnoreCase))
            .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}