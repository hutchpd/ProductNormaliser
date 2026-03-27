namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRunCandidateReason
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}