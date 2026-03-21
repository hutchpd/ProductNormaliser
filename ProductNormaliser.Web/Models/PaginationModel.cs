using System.Globalization;

namespace ProductNormaliser.Web.Models;

public sealed class PaginationModel
{
    public string PagePath { get; init; } = "/Index";
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public long TotalCount { get; init; }
    public IReadOnlyDictionary<string, string?> RouteValues { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;

    public Dictionary<string, string?> BuildRouteValues(int page)
    {
        var values = RouteValues.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        values["page"] = page.ToString(CultureInfo.InvariantCulture);
        return values;
    }
}