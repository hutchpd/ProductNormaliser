namespace ProductNormaliser.Core.Models;

public sealed class MergeConflict
{
    public string AttributeKey { get; set; } = default!;
    public object? ExistingValue { get; set; }
    public object? IncomingValue { get; set; }
    public string Reason { get; set; } = default!;
    public decimal Severity { get; set; }
}