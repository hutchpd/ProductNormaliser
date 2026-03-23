namespace ProductNormaliser.Web.Models;

public sealed class OperatorSummaryCardModel
{
    public string Title { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Tone { get; init; } = "neutral";
}