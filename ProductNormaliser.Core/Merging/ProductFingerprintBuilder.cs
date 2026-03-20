using System.Text;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Merging;

public sealed class ProductFingerprintBuilder
{
    public ProductFingerprint Build(SourceProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return Build(product.Gtin, product.Brand, product.ModelNumber, product.Title);
    }

    public ProductFingerprint Build(CanonicalProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return Build(product.Gtin, product.Brand, product.ModelNumber, product.DisplayName);
    }

    public decimal CalculateSimilarity(ProductFingerprint left, ProductFingerprint right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!string.IsNullOrWhiteSpace(left.Gtin) && string.Equals(left.Gtin, right.Gtin, StringComparison.OrdinalIgnoreCase))
        {
            return 1.00m;
        }

        var brandScore = left.BrandKey.Length > 0 && string.Equals(left.BrandKey, right.BrandKey, StringComparison.Ordinal)
            ? 1.00m
            : 0.00m;
        var modelScore = CalculateStringSimilarity(left.ModelKey, right.ModelKey);
        var titleScore = CalculateTokenSimilarity(left.Tokens, right.Tokens);

        if (brandScore == 1.00m && modelScore >= 0.90m && titleScore >= 0.45m)
        {
            return 0.91m;
        }

        return decimal.Round((brandScore * 0.20m) + (modelScore * 0.45m) + (titleScore * 0.35m), 4, MidpointRounding.AwayFromZero);
    }

    private ProductFingerprint Build(string? gtin, string? brand, string? modelNumber, string? title)
    {
        var brandKey = NormaliseText(brand);
        var modelKey = NormaliseText(modelNumber);
        var titleKey = NormaliseText(title);
        var signature = !string.IsNullOrWhiteSpace(gtin)
            ? gtin.Trim()
            : string.Join('|', new[] { brandKey, modelKey, titleKey }.Where(part => part.Length > 0));

        return new ProductFingerprint
        {
            Gtin = string.IsNullOrWhiteSpace(gtin) ? null : gtin.Trim(),
            BrandKey = brandKey,
            ModelKey = modelKey,
            TitleKey = titleKey,
            Signature = signature,
            Tokens = BuildTokens(brandKey, modelKey, titleKey)
        };
    }

    private static decimal CalculateStringSimilarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0.00m;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1.00m;
        }

        if (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal))
        {
            return 0.90m;
        }

        return CalculateTokenSimilarity(BuildTokens(left), BuildTokens(right));
    }

    private static decimal CalculateTokenSimilarity(IReadOnlyCollection<string> leftTokens, IReadOnlyCollection<string> rightTokens)
    {
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0.00m;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();

        return union == 0
            ? 0.00m
            : decimal.Round((decimal)intersection / union, 4, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyCollection<string> BuildTokens(params string[] values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormaliseText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

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

            if (!previousWasWhitespace)
            {
                builder.Append(' ');
                previousWasWhitespace = true;
            }
        }

        return builder.ToString().Trim();
    }
}