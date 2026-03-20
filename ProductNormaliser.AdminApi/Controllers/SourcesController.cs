using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/sources")]
public sealed class SourcesController(ISourceManagementService sourceManagementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SourceDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken = default)
    {
        var sources = await sourceManagementService.ListAsync(cancellationToken);
        return Ok(sources.Select(Map).ToArray());
    }

    [HttpGet("{sourceId}")]
    [ProducesResponseType(typeof(SourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSource(string sourceId, CancellationToken cancellationToken = default)
    {
        var source = await sourceManagementService.GetAsync(sourceId, cancellationToken);
        return source is null ? NotFound() : Ok(Map(source));
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
                ThrottlingPolicy = request.ThrottlingPolicy is null ? null : Map(request.ThrottlingPolicy)
            }, cancellationToken);

            return CreatedAtAction(nameof(GetSource), new { sourceId = source.Id }, Map(source));
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
                Description = request.Description
            }, cancellationToken);

            return Ok(Map(source));
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
            return Ok(Map(await sourceManagementService.EnableAsync(sourceId, cancellationToken)));
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
            return Ok(Map(await sourceManagementService.DisableAsync(sourceId, cancellationToken)));
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
            return Ok(Map(await sourceManagementService.AssignCategoriesAsync(sourceId, request.CategoryKeys, cancellationToken)));
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
            return Ok(Map(await sourceManagementService.SetThrottlingAsync(sourceId, new SourceThrottlingPolicy
            {
                MinDelayMs = request.MinDelayMs,
                MaxDelayMs = request.MaxDelayMs,
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                RequestsPerMinute = request.RequestsPerMinute,
                RespectRobotsTxt = request.RespectRobotsTxt
            }, cancellationToken)));
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

    private static SourceDto Map(CrawlSource source)
    {
        return new SourceDto
        {
            SourceId = source.Id,
            DisplayName = source.DisplayName,
            BaseUrl = source.BaseUrl,
            Host = source.Host,
            Description = source.Description,
            IsEnabled = source.IsEnabled,
            SupportedCategoryKeys = source.SupportedCategoryKeys.ToArray(),
            ThrottlingPolicy = new SourceThrottlingPolicyDto
            {
                MinDelayMs = source.ThrottlingPolicy.MinDelayMs,
                MaxDelayMs = source.ThrottlingPolicy.MaxDelayMs,
                MaxConcurrentRequests = source.ThrottlingPolicy.MaxConcurrentRequests,
                RequestsPerMinute = source.ThrottlingPolicy.RequestsPerMinute,
                RespectRobotsTxt = source.ThrottlingPolicy.RespectRobotsTxt
            },
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

    private static Dictionary<string, string[]> CreateValidationErrors(ArgumentException exception)
    {
        return new Dictionary<string, string[]>
        {
            [string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName] = [exception.Message]
        };
    }
}