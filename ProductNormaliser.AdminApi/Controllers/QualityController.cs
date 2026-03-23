using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/quality")]
public sealed class QualityController(IDataIntelligenceService dataIntelligenceService) : ControllerBase
{
    [HttpGet("coverage/detailed")]
    public async Task<IActionResult> GetDetailedCoverage([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetDetailedCoverageAsync(categoryKey, cancellationToken));
    }

    [HttpGet("unmapped")]
    public async Task<IActionResult> GetUnmappedAttributes([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetUnmappedAttributesAsync(categoryKey, cancellationToken));
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSourceQuality([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetSourceQualityScoresAsync(categoryKey, cancellationToken));
    }

    [HttpGet("merge-insights")]
    public async Task<IActionResult> GetMergeInsights([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetMergeInsightsAsync(categoryKey, cancellationToken));
    }

    [HttpGet("source-history")]
    public async Task<IActionResult> GetSourceHistory([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, [FromQuery] string? sourceName = null, [FromQuery] int? timeRangeDays = null, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetSourceHistoryAsync(categoryKey, sourceName, timeRangeDays, cancellationToken));
    }

    [HttpGet("attribute-stability")]
    public async Task<IActionResult> GetAttributeStability([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetAttributeStabilityAsync(categoryKey, cancellationToken));
    }

    [HttpGet("source-disagreements")]
    public async Task<IActionResult> GetSourceDisagreements([FromQuery] string categoryKey = TvCategorySchemaProvider.CategoryKey, [FromQuery] string? sourceName = null, [FromQuery] int? timeRangeDays = null, CancellationToken cancellationToken = default)
    {
        return Ok(await dataIntelligenceService.GetSourceDisagreementsAsync(categoryKey, sourceName, timeRangeDays, cancellationToken));
    }
}