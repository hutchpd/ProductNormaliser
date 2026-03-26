namespace ProductNormaliser.Application.AI;

public sealed class PageClassificationEvaluationSummary
{
    public int Total { get; init; }
    public double LlmAccuracy { get; init; }
    public double HeuristicAccuracy { get; init; }
    public double CombinedAccuracy { get; init; }
    public int FalsePositives { get; init; }
    public int FalseNegatives { get; init; }
}