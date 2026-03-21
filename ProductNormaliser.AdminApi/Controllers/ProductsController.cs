using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IAdminQueryService adminQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] string? category, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return Ok(await adminQueryService.ListProductsAsync(category, search, page, pageSize, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(string id, CancellationToken cancellationToken)
    {
        var product = await adminQueryService.GetProductAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetProductHistory(string id, CancellationToken cancellationToken)
    {
        return Ok(await adminQueryService.GetProductHistoryAsync(id, cancellationToken));
    }
}