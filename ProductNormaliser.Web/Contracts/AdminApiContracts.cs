namespace ProductNormaliser.Web.Contracts;

public sealed class CategoryMetadataDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FamilyKey { get; init; } = string.Empty;
    public string FamilyDisplayName { get; init; } = string.Empty;
    public string IconKey { get; init; } = string.Empty;
    public string CrawlSupportStatus { get; init; } = string.Empty;
    public decimal SchemaCompletenessScore { get; init; }
    public bool IsEnabled { get; init; }
}

public sealed class CategoryDetailDto
{
    public CategoryMetadataDto Metadata { get; init; } = new();
    public CategorySchemaDto Schema { get; init; } = new();
}

public sealed class CategorySchemaDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<CategorySchemaAttributeDto> Attributes { get; init; } = [];
}

public sealed class CategorySchemaAttributeDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public bool IsRequired { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class SourceDto
{
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceThrottlingPolicyDto ThrottlingPolicy { get; init; } = new();
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

public sealed class SourceThrottlingPolicyDto
{
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int RequestsPerMinute { get; init; }
    public bool RespectRobotsTxt { get; init; }
}

public sealed class RegisterSourceRequest
{
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceThrottlingPolicyDto? ThrottlingPolicy { get; init; }
}

public sealed class UpdateSourceRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class AssignSourceCategoriesRequest
{
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
}

public sealed class UpdateSourceThrottlingRequest
{
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int RequestsPerMinute { get; init; }
    public bool RespectRobotsTxt { get; init; } = true;
}