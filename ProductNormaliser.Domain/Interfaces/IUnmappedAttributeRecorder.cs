using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface IUnmappedAttributeRecorder
{
    void Record(string categoryKey, string canonicalKey, SourceAttributeValue rawAttribute);
}