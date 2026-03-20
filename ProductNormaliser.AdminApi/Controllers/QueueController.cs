using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/queue")]
public sealed class QueueController(IAdminQueryService adminQueryService, ICrawlPriorityService crawlPriorityService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        return Ok(await adminQueryService.GetQueueAsync(cancellationToken));
    }

    [HttpGet("priorities")]
    public async Task<IActionResult> GetQueuePriorities(CancellationToken cancellationToken)
    {
        var priorities = await crawlPriorityService.GetPrioritiesAsync(DateTime.UtcNow, cancellationToken);
        return Ok(priorities.Select(priority => new QueuePriorityDto
        {
            Id = priority.QueueItem.Id,
            SourceName = priority.QueueItem.SourceName,
            SourceUrl = priority.QueueItem.SourceUrl,
            CategoryKey = priority.QueueItem.CategoryKey,
            PriorityScore = priority.PriorityScore,
            SourceQualityScore = priority.SourceQualityScore,
            ChangeFrequencyScore = priority.ChangeFrequencyScore,
            PriceVolatilityScore = priority.PriceVolatilityScore,
            SpecStabilityScore = priority.SpecStabilityScore,
            MissingAttributeScore = priority.MissingAttributeScore,
            StalenessScore = priority.StalenessScore,
            MissingAttributeCount = priority.MissingAttributeCount,
            NextAttemptUtc = priority.QueueItem.NextAttemptUtc,
            EnqueuedUtc = priority.QueueItem.EnqueuedUtc,
            LastCrawledUtc = priority.LastCrawledUtc,
            Reasons = priority.Reasons
        }));
    }
}