using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface ICrawlBackoffService
{
    DateTime ComputeNextAttempt(CrawlContext context, SourceQualitySnapshot? sourceHistory, PageVolatilityProfile volatility);
    AdaptiveCrawlPolicy BuildPolicy(CrawlContext context, SourceQualitySnapshot? sourceHistory, PageVolatilityProfile volatility);
}