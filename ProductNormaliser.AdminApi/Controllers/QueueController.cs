using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/queue")]
public sealed class QueueController(IAdminQueryService adminQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        return Ok(await adminQueryService.GetQueueAsync(cancellationToken));
    }
}