namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateDiscoveryDiagnostic
{
    public const string SeverityInfo = "info";
    public const string SeverityWarning = "warning";
    public const string SeverityError = "error";

    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = SeverityInfo;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}