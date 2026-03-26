using System.Reflection;

namespace ProductNormaliser.Tests;

internal static class EmbeddedHtmlFixtureLoader
{
    public static string Load(string fileName)
    {
        return LoadBySuffix($".Fixtures.{fileName}");
    }

    public static string LoadTestData(string relativePath)
    {
        var suffix = ".TestData." + relativePath.Replace('/', '.').Replace('\\', '.');
        return LoadBySuffix(suffix);
    }

    public static IReadOnlyList<string> ListTestData(string relativeFolder)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var suffix = ".TestData." + relativeFolder.Replace('/', '.').Replace('\\', '.').TrimEnd('.') + ".";

        return assembly
            .GetManifestResourceNames()
            .Where(name => name.Contains(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(name => name[(name.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase) + suffix.Length)..])
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string LoadBySuffix(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}