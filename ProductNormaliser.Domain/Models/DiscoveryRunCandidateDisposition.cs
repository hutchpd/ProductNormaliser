namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRunCandidateDisposition
{
    public string Id { get; set; } = default!;
    public string State { get; set; } = DiscoveryRunCandidateStates.Dismissed;
    public string ScopeFingerprint { get; set; } = string.Empty;
    public IReadOnlyList<string> RequestedCategoryKeys { get; set; } = [];
    public string? Market { get; set; }
    public string? Locale { get; set; }
    public string NormalizedHost { get; set; } = string.Empty;
    public string NormalizedBaseUrl { get; set; } = string.Empty;
    public string NormalizedDisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedMarkets { get; set; } = [];
    public string SourceRunId { get; set; } = string.Empty;
    public string SourceCandidateKey { get; set; } = string.Empty;
    public string? SupersededByCandidateKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? RestoredUtc { get; set; }
}