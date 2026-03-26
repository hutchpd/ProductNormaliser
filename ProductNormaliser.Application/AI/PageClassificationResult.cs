namespace ProductNormaliser.Application.AI;

public class PageClassificationResult
{
    public bool IsProductPage { get; set; }
    public bool HasSpecifications { get; set; }
    public string? DetectedCategory { get; set; }
    public double Confidence { get; set; }
    public string LlmStatus { get; set; } = LlmStatusCodes.Active;
    public string? LlmStatusMessage { get; set; }
    public string? Reason { get; set; }
}
