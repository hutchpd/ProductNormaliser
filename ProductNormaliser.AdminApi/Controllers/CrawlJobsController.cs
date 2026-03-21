using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/crawl/jobs")]
public sealed class CrawlJobsController(ICrawlJobService crawlJobService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CrawlJobListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetJobs([FromQuery] string? status, [FromQuery] string? requestType, [FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await crawlJobService.ListAsync(new CrawlJobQuery
            {
                Status = status,
                RequestType = requestType,
                CategoryKey = category,
                Page = page,
                PageSize = pageSize
            }, cancellationToken);

            return Ok(new CrawlJobListResponse
            {
                Items = jobs.Items.Select(Map).ToArray(),
                Page = jobs.Page,
                PageSize = jobs.PageSize,
                TotalCount = jobs.TotalCount,
                TotalPages = jobs.TotalPages
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(exception.ParamName) ? "query" : exception.ParamName] = [exception.Message]
            }));
        }
    }

    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(CrawlJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(string jobId, CancellationToken cancellationToken)
    {
        var job = await crawlJobService.GetAsync(jobId, cancellationToken);
        return job is null ? NotFound() : Ok(Map(job));
    }

    [HttpPost("{jobId}/cancel")]
    [ProducesResponseType(typeof(CrawlJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelJob(string jobId, CancellationToken cancellationToken)
    {
        var job = await crawlJobService.CancelAsync(jobId, cancellationToken);
        return job is null ? NotFound() : Ok(Map(job));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CrawlJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJob([FromBody] Contracts.CreateCrawlJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await crawlJobService.CreateAsync(new Application.Crawls.CreateCrawlJobRequest
            {
                RequestType = request.RequestType,
                RequestedCategories = request.RequestedCategories,
                RequestedSources = request.RequestedSources,
                RequestedProductIds = request.RequestedProductIds
            }, cancellationToken);

            return CreatedAtAction(nameof(GetJob), new { jobId = job.JobId }, Map(job));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName] = [exception.Message]
            }));
        }
    }

    private static CrawlJobDto Map(CrawlJob job)
    {
        return new CrawlJobDto
        {
            JobId = job.JobId,
            RequestType = job.RequestType,
            RequestedCategories = job.RequestedCategories,
            RequestedSources = job.RequestedSources,
            RequestedProductIds = job.RequestedProductIds,
            TotalTargets = job.TotalTargets,
            ProcessedTargets = job.ProcessedTargets,
            SuccessCount = job.SuccessCount,
            SkippedCount = job.SkippedCount,
            FailedCount = job.FailedCount,
            CancelledCount = job.CancelledCount,
            StartedAt = job.StartedAt,
            LastUpdatedAt = job.LastUpdatedAt,
            EstimatedCompletion = job.EstimatedCompletion,
            Status = job.Status,
            PerCategoryBreakdown = job.PerCategoryBreakdown.Select(item => new CrawlJobCategoryBreakdownDto
            {
                CategoryKey = item.CategoryKey,
                TotalTargets = item.TotalTargets,
                ProcessedTargets = item.ProcessedTargets,
                SuccessCount = item.SuccessCount,
                SkippedCount = item.SkippedCount,
                FailedCount = item.FailedCount,
                CancelledCount = item.CancelledCount
            }).ToArray()
        };
    }
}