using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Normalisation;

public sealed class NullUnmappedAttributeRecorder : IUnmappedAttributeRecorder
{
    public static NullUnmappedAttributeRecorder Instance { get; } = new();

    private NullUnmappedAttributeRecorder()
    {
    }

    public void Record(string categoryKey, string canonicalKey, SourceAttributeValue rawAttribute)
    {
    }
}