namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateDiscoveryResponseDto
{
    public IReadOnlyList<string> RequestedCategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public string AutomationMode { get; init; } = string.Empty;
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public string LlmStatus { get; init; } = string.Empty;
    public string LlmStatusMessage { get; init; } = string.Empty;
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<SourceCandidateDiscoveryDiagnosticDto> Diagnostics { get; init; } = [];
    public IReadOnlyList<SourceCandidateDto> Candidates { get; init; } = [];
}

public sealed class SourceCandidateDiscoveryDiagnosticDto
{
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}