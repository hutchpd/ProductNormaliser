namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class SemanticDeltaResult
{
    public bool HasMeaningfulChanges { get; init; }
    public bool HasAttributeChanges { get; init; }
    public bool HasOfferChanges { get; init; }
    public bool PriceChanged { get; init; }
    public bool AvailabilityChanged { get; init; }
    public IReadOnlyList<string> ChangedAttributeKeys { get; init; } = [];
    public IReadOnlyList<SemanticChangeDetail> ChangeDetails { get; init; } = [];
    public string Summary { get; init; } = "No semantic changes detected.";
}