using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface IProductIdentityResolver
{
    ProductIdentityMatchResult Match(
        SourceProduct sourceProduct,
        IReadOnlyCollection<CanonicalProduct> candidates);
}