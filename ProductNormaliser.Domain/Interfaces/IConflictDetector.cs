using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface IConflictDetector
{
    List<MergeConflict> Detect(CanonicalProduct product);
}