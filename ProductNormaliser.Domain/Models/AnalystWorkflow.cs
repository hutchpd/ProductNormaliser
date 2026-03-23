namespace ProductNormaliser.Core.Models;

public sealed class AnalystWorkflow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string RoutePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PrimaryCategoryKey { get; set; }
    public List<string> SelectedCategoryKeys { get; set; } = [];
    public Dictionary<string, string> State { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}