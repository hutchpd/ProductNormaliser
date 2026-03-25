namespace ProductNormaliser.Core.Models;

public sealed class SourceAutomationPolicy
{
    public string Mode { get; set; } = SourceAutomationModes.OperatorAssisted;
}