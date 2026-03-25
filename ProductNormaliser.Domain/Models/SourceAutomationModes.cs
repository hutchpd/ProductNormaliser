namespace ProductNormaliser.Core.Models;

public static class SourceAutomationModes
{
    public const string OperatorAssisted = "operator_assisted";
    public const string SuggestAccept = "suggest_accept";
    public const string AutoAcceptAndSeed = "auto_accept_and_seed";

    public static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            SuggestAccept => SuggestAccept,
            AutoAcceptAndSeed => AutoAcceptAndSeed,
            _ => OperatorAssisted
        };
    }

    public static bool IsSupported(string? value)
    {
        var normalized = Normalize(value);
        return normalized is OperatorAssisted or SuggestAccept or AutoAcceptAndSeed;
    }
}