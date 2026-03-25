namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateReason
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public decimal Weight { get; init; }
}