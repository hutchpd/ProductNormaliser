namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UpsertAnalystWorkflowRequest
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string WorkflowType { get; init; } = string.Empty;
    public string RoutePath { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? PrimaryCategoryKey { get; init; }
    public IReadOnlyList<string> SelectedCategoryKeys { get; init; } = [];
    public IReadOnlyDictionary<string, string> State { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}