using System.Text;

namespace ProductNormaliser.Core.Normalisation;

public sealed class AttributeNameNormaliser
{
    public string Normalise(string? attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(attributeName.Trim().Length);

        foreach (var character in attributeName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (IsSeparator(character))
            {
                builder.Append(' ');
            }
        }

        return CollapseWhitespace(builder.ToString());
    }

    private static bool IsSeparator(char character)
    {
        return character is ' ' or '\t' or '\r' or '\n' or '_' or '-' or '/' or '\\' or '|';
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}