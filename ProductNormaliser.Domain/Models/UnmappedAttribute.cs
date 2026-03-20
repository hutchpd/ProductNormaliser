namespace ProductNormaliser.Core.Models;

public sealed class UnmappedAttribute
{
    public string Id { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string CanonicalKey { get; set; } = default!;
    public string RawAttributeKey { get; set; } = default!;
    public int OccurrenceCount { get; set; }
    public List<string> SourceNames { get; set; } = [];
    public List<string> SampleValues { get; set; } = [];
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public List<UnmappedAttributeObservation> RecentObservations { get; set; } = [];
}