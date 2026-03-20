using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class AttributeAliasDictionary
{
    private readonly AttributeNameNormaliser attributeNameNormaliser;
    private readonly Dictionary<string, string> aliases;

    public AttributeAliasDictionary(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        IEnumerable<CanonicalAttributeDefinition>? schemaAttributes = null,
        IReadOnlyDictionary<string, string>? explicitAliases = null)
    {
        this.attributeNameNormaliser = attributeNameNormaliser ?? new AttributeNameNormaliser();

        schemaAttributes ??= new TvCategorySchemaProvider().GetSchema().Attributes;
        explicitAliases ??= new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["screen size"] = "screen_size_inch",
            ["screensize"] = "screen_size_inch",
            ["display size"] = "screen_size_inch",
            ["hdmi ports"] = "hdmi_port_count",
            ["number of hdmi ports"] = "hdmi_port_count",
            ["smart tv"] = "smart_tv",
            ["refresh rate"] = "refresh_rate_hz"
        };

        aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var alias in explicitAliases)
        {
            aliases[this.attributeNameNormaliser.Normalise(alias.Key)] = alias.Value;
        }

        foreach (var attribute in schemaAttributes)
        {
            aliases.TryAdd(this.attributeNameNormaliser.Normalise(attribute.Key), attribute.Key);
            aliases.TryAdd(this.attributeNameNormaliser.Normalise(attribute.DisplayName), attribute.Key);
        }
    }

    public string? Resolve(string? rawAttributeName)
    {
        var normalisedName = attributeNameNormaliser.Normalise(rawAttributeName);

        return aliases.TryGetValue(normalisedName, out var canonicalKey)
            ? canonicalKey
            : null;
    }
}