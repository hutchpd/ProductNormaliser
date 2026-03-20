using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public interface ICategorySchemaProvider
{
    string SupportedCategoryKey { get; }
    CategorySchema GetSchema();
}