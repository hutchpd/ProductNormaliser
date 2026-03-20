namespace ProductNormaliser.AdminApi.Contracts;

public sealed class StatsResponse
{
    public int TotalCanonicalProducts { get; init; }
    public int TotalSourceProducts { get; init; }
    public decimal AverageAttributesPerProduct { get; init; }
    public decimal PercentProductsWithConflicts { get; init; }
    public decimal PercentProductsMissingKeyAttributes { get; init; }
}