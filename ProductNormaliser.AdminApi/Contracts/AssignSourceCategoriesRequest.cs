namespace ProductNormaliser.AdminApi.Contracts;

public sealed class AssignSourceCategoriesRequest
{
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
}