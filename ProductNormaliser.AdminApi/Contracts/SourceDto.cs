namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceDto
{
    public string SourceId { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string BaseUrl { get; init; } = default!;
    public string Host { get; init; } = default!;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceThrottlingPolicyDto ThrottlingPolicy { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}