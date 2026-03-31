using System.Text.Json;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class SolutionLaunchProfileTests
{
    [Test]
    public void SolutionLaunchProfile_StartsAdminApiWebAndWorker()
    {
        var profilePath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ProductNormaliser.slnLaunch.user"));
        Assert.That(File.Exists(profilePath), Is.True, $"Expected solution launch profile at '{profilePath}'.");

        using var document = JsonDocument.Parse(File.ReadAllText(profilePath));
        var profiles = document.RootElement;
        Assert.That(profiles.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(profiles.GetArrayLength(), Is.GreaterThan(0));

        var firstProfile = profiles[0];
        Assert.That(firstProfile.TryGetProperty("Projects", out var projects), Is.True);

        var startedProjects = projects
            .EnumerateArray()
            .Where(project => project.TryGetProperty("Action", out var action)
                && string.Equals(action.GetString(), "Start", StringComparison.OrdinalIgnoreCase))
            .Select(project => project.GetProperty("Path").GetString())
            .OfType<string>()
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(startedProjects, Does.Contain("ProductNormaliser.AdminApi\\ProductNormaliser.AdminApi.csproj"));
            Assert.That(startedProjects, Does.Contain("ProductNormaliser.Web\\ProductNormaliser.Web.csproj"));
            Assert.That(startedProjects, Does.Contain("ProductNormaliser.Worker\\ProductNormaliser.Worker.csproj"));
        });
    }
}