namespace ProductNormaliser.AdminApi.Contracts;

public sealed class DetailedCoverageResponse
{
    public string CategoryKey { get; init; } = default!;
    public int TotalCanonicalProducts { get; init; }
    public int TotalSourceProducts { get; init; }
    public IReadOnlyList<AttributeCoverageDetailDto> Attributes { get; init; } = [];
    public IReadOnlyList<AttributeGapDto> MostMissingAttributes { get; init; } = [];
    public IReadOnlyList<AttributeGapDto> MostConflictedAttributes { get; init; } = [];
}