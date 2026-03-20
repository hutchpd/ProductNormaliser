using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface ICanonicalMergeService
{
    CanonicalProduct Merge(CanonicalProduct? existing, SourceProduct incoming);
}