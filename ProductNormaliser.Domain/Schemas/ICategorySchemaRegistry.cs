using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public interface ICategorySchemaRegistry
{
    ICategorySchemaProvider? GetProvider(string categoryKey);
    CategorySchema? GetSchema(string categoryKey);
    bool TryGetSchema(string categoryKey, out CategorySchema schema);
}