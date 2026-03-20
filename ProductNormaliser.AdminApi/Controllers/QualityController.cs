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
}