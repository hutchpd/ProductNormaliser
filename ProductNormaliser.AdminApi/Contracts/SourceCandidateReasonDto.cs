namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateReasonDto
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public decimal Weight { get; init; }
}