using System.Security.Cryptography;
using System.Text;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class DeltaProcessor(IRawPageStore rawPageStore) : IDeltaProcessor
{
    public string ComputeHash(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(html));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken)
    {
        var contentHash = ComputeHash(html);
        var latestPage = await rawPageStore.GetLatestBySourceAsync(sourceName, sourceUrl, cancellationToken);

        return new DeltaDetectionResult
        {
            ContentHash = contentHash,
            IsUnchanged = latestPage is not null && string.Equals(latestPage.ContentHash, contentHash, StringComparison.Ordinal)
        };
    }
}