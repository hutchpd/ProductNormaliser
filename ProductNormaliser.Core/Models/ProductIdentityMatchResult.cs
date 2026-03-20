namespace ProductNormaliser.Core.Models;

public sealed class ProductIdentityMatchResult
{
    public string? CanonicalProductId { get; set; }
    public bool IsMatch { get; set; }
    public decimal Confidence { get; set; }
    public string? MatchReason { get; set; }
}