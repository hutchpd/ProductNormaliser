namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateDiscoveryResult
{
    public IReadOnlyList<string> RequestedCategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public string AutomationMode { get; init; } = string.Empty;
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public string LlmStatus { get; init; } = ProductNormaliser.Application.AI.LlmStatusCodes.Disabled;
    public string LlmStatusMessage { get; init; } = string.Empty;
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<SourceCandidateDiscoveryDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<SourceCandidateResult> Candidates { get; init; } = [];
}