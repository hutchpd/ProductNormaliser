using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.StructuredData;

public interface ISourceProductBuilder
{
    SourceProduct Build(string sourceName, string categoryKey, ExtractedStructuredProduct extractedProduct, DateTime fetchedUtc);
}