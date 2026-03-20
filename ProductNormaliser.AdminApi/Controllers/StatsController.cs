using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(IAdminQueryService adminQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        return Ok(await adminQueryService.GetStatsAsync(cancellationToken));
    }
}