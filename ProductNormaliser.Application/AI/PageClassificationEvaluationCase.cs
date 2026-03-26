namespace ProductNormaliser.Application.AI;

public sealed class PageClassificationEvaluationCase
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool ExpectedIsProduct { get; set; }
}