namespace ProductNormaliser.Infrastructure.Crawling;

public interface ICrawlPriorityService
{
    Task<IReadOnlyList<CrawlPriorityAssessment>> GetPrioritiesAsync(DateTime utcNow, CancellationToken cancellationToken);
}