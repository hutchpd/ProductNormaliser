using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/crawl")]
public sealed class CrawlController(IAdminQueryService adminQueryService) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(CancellationToken cancellationToken)
    {
        return Ok(await adminQueryService.GetCrawlLogsAsync(cancellationToken));
    }

    [HttpGet("logs/{id}")]
    public async Task<IActionResult> GetLog(string id, CancellationToken cancellationToken)
    {
        var log = await adminQueryService.GetCrawlLogAsync(id, cancellationToken);
        return log is null ? NotFound() : Ok(log);
    }
}