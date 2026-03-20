using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/conflicts")]
public sealed class ConflictsController(IAdminQueryService adminQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetConflicts(CancellationToken cancellationToken)
    {
        return Ok(await adminQueryService.GetConflictsAsync(cancellationToken));
    }
}