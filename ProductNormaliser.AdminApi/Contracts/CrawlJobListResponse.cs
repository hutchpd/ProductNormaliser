namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CrawlJobListResponse
{
    public IReadOnlyList<CrawlJobDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }
}