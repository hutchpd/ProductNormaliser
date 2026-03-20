namespace ProductNormaliser.Core.Models;

public sealed class UnmappedAttributeObservation
{
    public string SourceName { get; set; } = default!;
    public string? SourcePath { get; set; }
    public string? RawValue { get; set; }
    public DateTime ObservedUtc { get; set; }
}