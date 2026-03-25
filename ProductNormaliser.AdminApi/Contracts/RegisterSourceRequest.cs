using System.ComponentModel.DataAnnotations;

namespace ProductNormaliser.AdminApi.Contracts;

public sealed class RegisterSourceRequest
{
    [Required]
    public string SourceId { get; init; } = default!;

    [Required]
    public string DisplayName { get; init; } = default!;

    [Required]
    public string BaseUrl { get; init; } = default!;

    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = true;

    public IReadOnlyList<string> AllowedMarkets { get; init; } = [];

    public string? PreferredLocale { get; init; }

    public SourceAutomationPolicyDto? AutomationPolicy { get; init; }

    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];

    public SourceDiscoveryProfileDto? DiscoveryProfile { get; init; }

    public SourceThrottlingPolicyDto? ThrottlingPolicy { get; init; }
}