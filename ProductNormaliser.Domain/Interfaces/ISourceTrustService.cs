using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface ISourceTrustService
{
    decimal GetHistoricalTrustScore(string sourceName, string categoryKey);
    IReadOnlyList<SourceQualitySnapshot> GetSourceHistory(string categoryKey, string? sourceName = null, int? timeRangeDays = null, int limit = 30);
    void CaptureSnapshot(string sourceName, string categoryKey);
}