namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UnmappedAttributeDto
{
    public string CanonicalKey { get; init; } = default!;
    public string RawAttributeKey { get; init; } = default!;
    public int OccurrenceCount { get; init; }
    public IReadOnlyList<string> SourceNames { get; init; } = [];
    public IReadOnlyList<string> SampleValues { get; init; } = [];
    public DateTime LastSeenUtc { get; init; }
}