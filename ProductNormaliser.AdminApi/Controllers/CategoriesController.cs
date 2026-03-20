using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryManagementService categoryManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken = default)
    {
        var categories = await categoryManagementService.ListAsync(cancellationToken);
        return Ok(categories.Select(Map).ToArray());
    }

    [HttpGet("families")]
    public async Task<IActionResult> GetFamilies(CancellationToken cancellationToken = default)
    {
        var families = await categoryManagementService.ListFamiliesAsync(cancellationToken);
        return Ok(families.Select(family => new CategoryFamilyDto
        {
            FamilyKey = family.FamilyKey,
            FamilyDisplayName = family.FamilyDisplayName,
            Categories = family.Categories.Select(Map).ToArray()
        }).ToArray());
    }

    [HttpGet("enabled")]
    public async Task<IActionResult> GetEnabledCategories(CancellationToken cancellationToken = default)
    {
        var categories = await categoryManagementService.ListEnabledAsync(cancellationToken);
        return Ok(categories.Select(Map).ToArray());
    }

    [HttpGet("{categoryKey}")]
    public async Task<IActionResult> GetCategory(string categoryKey, CancellationToken cancellationToken = default)
    {
        var category = await categoryManagementService.GetAsync(categoryKey, cancellationToken);
        return category is null ? NotFound() : Ok(Map(category));
    }

    [HttpGet("{categoryKey}/detail")]
    public async Task<IActionResult> GetCategoryDetail(string categoryKey, CancellationToken cancellationToken = default)
    {
        var detail = await categoryManagementService.GetDetailAsync(categoryKey, cancellationToken);
        return detail is null
            ? NotFound()
            : Ok(new CategoryDetailDto
            {
                Metadata = Map(detail.Metadata),
                Schema = Map(detail.Schema)
            });
    }

    [HttpGet("{categoryKey}/schema")]
    public async Task<IActionResult> GetCategorySchema(string categoryKey, CancellationToken cancellationToken = default)
    {
        var schema = await categoryManagementService.GetSchemaAsync(categoryKey, cancellationToken);
        return schema is null
            ? NotFound()
            : Ok(Map(schema));
    }

    [HttpPut("{categoryKey}")]
    public async Task<IActionResult> UpsertCategory(string categoryKey, [FromBody] UpsertCategoryMetadataRequest request, CancellationToken cancellationToken = default)
    {
        var category = await categoryManagementService.UpsertAsync(new CategoryMetadata
        {
            CategoryKey = categoryKey,
            DisplayName = request.DisplayName,
            FamilyKey = request.FamilyKey,
            FamilyDisplayName = request.FamilyDisplayName,
            IconKey = request.IconKey,
            CrawlSupportStatus = ParseSupportStatus(request.CrawlSupportStatus),
            SchemaCompletenessScore = request.SchemaCompletenessScore,
            IsEnabled = request.IsEnabled
        }, cancellationToken);

        return Ok(Map(category));
    }

    private static CrawlSupportStatus ParseSupportStatus(string value)
    {
        return Enum.TryParse<CrawlSupportStatus>(value, ignoreCase: true, out var supportStatus)
            ? supportStatus
            : throw new ArgumentException($"Unsupported crawl support status '{value}'.", nameof(value));
    }

    private static CategoryMetadataDto Map(CategoryMetadata category)
    {
        return new CategoryMetadataDto
        {
            CategoryKey = category.CategoryKey,
            DisplayName = category.DisplayName,
            FamilyKey = category.FamilyKey,
            FamilyDisplayName = category.FamilyDisplayName,
            IconKey = category.IconKey,
            CrawlSupportStatus = category.CrawlSupportStatus.ToString(),
            SchemaCompletenessScore = category.SchemaCompletenessScore,
            IsEnabled = category.IsEnabled
        };
    }

    private static CategorySchemaDto Map(CategorySchema schema)
    {
        return new CategorySchemaDto
        {
            CategoryKey = schema.CategoryKey,
            DisplayName = schema.DisplayName,
            Attributes = schema.Attributes.Select(attribute => new CategorySchemaAttributeDto
            {
                Key = attribute.Key,
                DisplayName = attribute.DisplayName,
                ValueType = attribute.ValueType,
                Unit = attribute.Unit,
                IsRequired = attribute.IsRequired,
                Description = attribute.Description
            }).ToArray()
        };
    }
}