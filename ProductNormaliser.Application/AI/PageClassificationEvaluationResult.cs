namespace ProductNormaliser.Application.AI;

public class PageClassificationEvaluationResult
{
    public string Name { get; set; } = string.Empty;
    public bool ExpectedIsProduct { get; set; }
    public bool LlmIsProduct { get; set; }
    public bool HeuristicIsProduct { get; set; }
    public bool FinalDecision { get; set; }
    public double Confidence { get; set; }
    public bool FalsePositive { get; set; }
    public bool FalseNegative { get; set; }
    public string? Reason { get; set; }
}