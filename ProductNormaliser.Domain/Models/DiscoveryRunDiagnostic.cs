namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRunDiagnostic
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}