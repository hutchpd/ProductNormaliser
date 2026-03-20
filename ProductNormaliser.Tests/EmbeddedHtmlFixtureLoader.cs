using System.Reflection;

namespace ProductNormaliser.Tests;

internal static class EmbeddedHtmlFixtureLoader
{
    public static string Load(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith($".Fixtures.{fileName}", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}