using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/sources")]
public sealed class SourcesController(
    ISourceManagementService sourceManagementService,
    ISourceOperationalInsightsProvider sourceOperationalInsightsProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SourceDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken = default)
    {
        var sources = await sourceManagementService.ListAsync(cancellationToken);
        var insights = await sourceOperationalInsightsProvider.BuildAsync(sources, cancellationToken);
        return Ok(sources.Select(source => Map(source, insights)).ToArray());
    }

    [HttpGet("{sourceId}")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSource(string sourceId, CancellationToken cancellationToken = default)
    {
        var source = await sourceManagementService.GetAsync(sourceId, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
        return Ok(Map(source, insights));
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterSource([FromBody] RegisterSourceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await sourceManagementService.RegisterAsync(new CrawlSourceRegistration
            {
                SourceId = request.SourceId,
                DisplayName = request.DisplayName,
                BaseUrl = request.BaseUrl,
                Description = request.Description,
                IsEnabled = request.IsEnabled,
                SupportedCategoryKeys = request.SupportedCategoryKeys,
                DiscoveryProfile = request.DiscoveryProfile is null ? null : Map(request.DiscoveryProfile),
                ThrottlingPolicy = request.ThrottlingPolicy is null ? null : Map(request.ThrottlingPolicy)
            }, cancellationToken);

            var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
            return CreatedAtAction(nameof(GetSource), new { sourceId = source.Id }, Map(source, insights));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationErrors(exception)));
        }
    }

    [HttpPut("{sourceId}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSource(string sourceId, [FromBody] UpdateSourceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await sourceManagementService.UpdateAsync(sourceId, new CrawlSourceUpdate
            {
                DisplayName = request.DisplayName,
                BaseUrl = request.BaseUrl,
                Description = request.Description,
                DiscoveryProfile = request.DiscoveryProfile is null ? null : Map(request.DiscoveryProfile)
            }, cancellationToken);

            var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
            return Ok(Map(source, insights));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationErrors(exception)));
        }
    }

    [HttpPost("{sourceId}/enable")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableSource(string sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await sourceManagementService.EnableAsync(sourceId, cancellationToken);
            var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
            return Ok(Map(source, insights));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{sourceId}/disable")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableSource(string sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await sourceManagementService.DisableAsync(sourceId, cancellationToken);
            var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
            return Ok(Map(source, insights));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{sourceId}/categories")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignCategories(string sourceId, [FromBody] AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await sourceManagementService.AssignCategoriesAsync(sourceId, request.CategoryKeys, cancellationToken);
            var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
            return Ok(Map(source, insights));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationErrors(exception)));
        }
    }

    [HttpPut("{sourceId}/throttling")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateThrottling(string sourceId, [FromBody] UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await sourceManagementService.SetThrottlingAsync(sourceId, new SourceThrottlingPolicy
            {
                MinDelayMs = request.MinDelayMs,
                MaxDelayMs = request.MaxDelayMs,
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                RequestsPerMinute = request.RequestsPerMinute,
                RespectRobotsTxt = request.RespectRobotsTxt
            }, cancellationToken);
            var insights = await sourceOperationalInsightsProvider.BuildAsync([source], cancellationToken);
            return Ok(Map(source, insights));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationErrors(exception)));
        }
    }

    private static SourceDto Map(CrawlSource source, IReadOnlyDictionary<string, SourceOperationalInsights> insights)
    {
        insights.TryGetValue(source.Id, out var operationalInsights);
        operationalInsights ??= new SourceOperationalInsights();

        return new SourceDto
        {
            SourceId = source.Id,
            DisplayName = source.DisplayName,
            BaseUrl = source.BaseUrl,
            Host = source.Host,
            Description = source.Description,
            IsEnabled = source.IsEnabled,
            SupportedCategoryKeys = source.SupportedCategoryKeys.ToArray(),
            DiscoveryProfile = new SourceDiscoveryProfileDto
            {
                CategoryEntryPages = source.DiscoveryProfile.CategoryEntryPages.ToDictionary(
                    entry => entry.Key,
                    entry => (IReadOnlyList<string>)entry.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
                SitemapHints = source.DiscoveryProfile.SitemapHints.ToArray(),
                AllowedPathPrefixes = source.DiscoveryProfile.AllowedPathPrefixes.ToArray(),
                ExcludedPathPrefixes = source.DiscoveryProfile.ExcludedPathPrefixes.ToArray(),
                ProductUrlPatterns = source.DiscoveryProfile.ProductUrlPatterns.ToArray(),
                ListingUrlPatterns = source.DiscoveryProfile.ListingUrlPatterns.ToArray(),
                MaxDiscoveryDepth = source.DiscoveryProfile.MaxDiscoveryDepth,
                MaxUrlsPerRun = source.DiscoveryProfile.MaxUrlsPerRun
            },
            ThrottlingPolicy = new SourceThrottlingPolicyDto
            {
                MinDelayMs = source.ThrottlingPolicy.MinDelayMs,
                MaxDelayMs = source.ThrottlingPolicy.MaxDelayMs,
                MaxConcurrentRequests = source.ThrottlingPolicy.MaxConcurrentRequests,
                RequestsPerMinute = source.ThrottlingPolicy.RequestsPerMinute,
                RespectRobotsTxt = source.ThrottlingPolicy.RespectRobotsTxt
            },
            Readiness = operationalInsights.Readiness,
            Health = operationalInsights.Health,
            LastActivity = operationalInsights.LastActivity,
            DiscoveryQueueDepth = operationalInsights.DiscoveryQueueDepth,
            ListingPagesVisitedLast24Hours = operationalInsights.ListingPagesVisitedLast24Hours,
            SitemapUrlsProcessedLast24Hours = operationalInsights.SitemapUrlsProcessedLast24Hours,
            ConfirmedProductUrlsLast24Hours = operationalInsights.ConfirmedProductUrlsLast24Hours,
            DiscoveryCoverageByCategory = operationalInsights.DiscoveryCoverageByCategory,
            LastDiscoveryUtc = operationalInsights.LastDiscoveryUtc,
            SitemapReachable = operationalInsights.SitemapReachable,
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = source.UpdatedUtc
        };
    }

    private static SourceThrottlingPolicy Map(SourceThrottlingPolicyDto throttlingPolicy)
    {
        return new SourceThrottlingPolicy
        {
            MinDelayMs = throttlingPolicy.MinDelayMs,
            MaxDelayMs = throttlingPolicy.MaxDelayMs,
            MaxConcurrentRequests = throttlingPolicy.MaxConcurrentRequests,
            RequestsPerMinute = throttlingPolicy.RequestsPerMinute,
            RespectRobotsTxt = throttlingPolicy.RespectRobotsTxt
        };
    }

    private static SourceDiscoveryProfile Map(SourceDiscoveryProfileDto discoveryProfile)
    {
        return new SourceDiscoveryProfile
        {
            CategoryEntryPages = discoveryProfile.CategoryEntryPages.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
                StringComparer.OrdinalIgnoreCase),
            SitemapHints = discoveryProfile.SitemapHints.Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            AllowedPathPrefixes = discoveryProfile.AllowedPathPrefixes.Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            ExcludedPathPrefixes = discoveryProfile.ExcludedPathPrefixes.Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            ProductUrlPatterns = discoveryProfile.ProductUrlPatterns.Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            ListingUrlPatterns = discoveryProfile.ListingUrlPatterns.Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            MaxDiscoveryDepth = discoveryProfile.MaxDiscoveryDepth,
            MaxUrlsPerRun = discoveryProfile.MaxUrlsPerRun
        };
    }

    private static Dictionary<string, string[]> CreateValidationErrors(ArgumentException exception)
    {
        return new Dictionary<string, string[]>
        {
            [string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName] = [exception.Message]
        };
    }
}