using System.Text;

namespace ProductNormaliser.Core.Normalisation;

public sealed class ValueMappingRegistry
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> mappings = new(StringComparer.Ordinal)
    {
        ["display_technology"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oled"] = "OLED",
            ["qled"] = "QLED",
            ["led"] = "LED"
        },
        ["native_resolution"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["4k ultra hd"] = "4K",
            ["ultra hd"] = "4K",
            ["full hd"] = "1080p",
            ["hd ready"] = "720p"
        }
    };

    public bool TryMap(string canonicalKey, string? rawValue, out string mappedValue)
    {
        mappedValue = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (!mappings.TryGetValue(canonicalKey, out var values))
        {
            return false;
        }

        if (!values.TryGetValue(NormaliseValue(rawValue), out var resolvedValue))
        {
            return false;
        }

        mappedValue = resolvedValue;
        return true;
    }

    private static string NormaliseValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasWhitespace = false;
                continue;
            }

            if (char.IsWhiteSpace(character) || character is '_' or '-' or '/')
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
            }
        }

        return builder.ToString().Trim();
    }
}