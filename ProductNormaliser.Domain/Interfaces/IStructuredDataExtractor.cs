using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface IStructuredDataExtractor
{
    IReadOnlyCollection<ExtractedStructuredProduct> ExtractProducts(string html, string url);
}