using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class AttributeAliasDictionary
{
    private readonly AttributeNameNormaliser attributeNameNormaliser;
    private readonly Dictionary<string, string> aliases;

    public AttributeAliasDictionary(AttributeNameNormaliser? attributeNameNormaliser = null)
    {
        this.attributeNameNormaliser = attributeNameNormaliser ?? new AttributeNameNormaliser();

        aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["screen size"] = "screen_size_inch",
            ["screensize"] = "screen_size_inch",
            ["display size"] = "screen_size_inch",
            ["hdmi ports"] = "hdmi_port_count",
            ["number of hdmi ports"] = "hdmi_port_count",
            ["smart tv"] = "smart_tv",
            ["refresh rate"] = "refresh_rate_hz"
        };

        foreach (var attribute in new TvCategorySchemaProvider().GetSchema().Attributes)
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