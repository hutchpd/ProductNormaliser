using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Crawls;

public sealed class CrawlJobQuery
{
    public string? Status { get; init; }
    public string? RequestType { get; init; }
    public string? CategoryKey { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class CrawlJobPage
{
    public IReadOnlyList<CrawlJob> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 || TotalCount == 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}