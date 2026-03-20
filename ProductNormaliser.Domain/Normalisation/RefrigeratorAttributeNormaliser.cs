using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class RefrigeratorAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public RefrigeratorAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            RefrigeratorCategorySchemaProvider.CategoryKey,
            new RefrigeratorCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["brand", "model_number", "total_capacity_litre", "installation_type", "energy_rating"],
            completenessAttributeKeys: ["brand", "model_number", "total_capacity_litre", "energy_rating", "frost_free", "installation_type"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["capacity"] = "total_capacity_litre",
                ["total capacity"] = "total_capacity_litre",
                ["fridge capacity"] = "fridge_capacity_litre",
                ["freezer capacity"] = "freezer_capacity_litre",
                ["energy class"] = "energy_rating",
                ["energy efficiency class"] = "energy_rating",
                ["frost free"] = "frost_free",
                ["installation type"] = "installation_type",
                ["width"] = "width_mm",
                ["height"] = "height_mm",
                ["depth"] = "depth_mm"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["installation_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["freestanding"] = "Freestanding",
                    ["built-in"] = "Built-In",
                    ["integrated"] = "Integrated"
                }
            },
            attributeNameNormaliser: attributeNameNormaliser,
            measurementParser: measurementParser,
            unitConversionService: unitConversionService,
            unmappedAttributeRecorder: unmappedAttributeRecorder)
    {
    }

    protected override bool TryNormaliseKnownAttribute(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition, out NormalisedAttributeValue normalisedValue)
    {
        normalisedValue = definition.Key switch
        {
            "total_capacity_litre" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "fridge_capacity_litre" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "freezer_capacity_litre" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "frost_free" => NormaliseBoolean(rawAttribute, definition),
            "width_mm" or "height_mm" or "depth_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}