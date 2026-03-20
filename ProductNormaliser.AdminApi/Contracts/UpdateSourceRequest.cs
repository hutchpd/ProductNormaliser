using System.ComponentModel.DataAnnotations;

namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UpdateSourceRequest
{
    [Required]
    public string DisplayName { get; init; } = default!;

    [Required]
    public string BaseUrl { get; init; } = default!;

    public string? Description { get; init; }
}