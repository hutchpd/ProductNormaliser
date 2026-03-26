namespace ProductNormaliser.Application.AI;

public static class LlmStatusCodes
{
    public const string Active = "active";
    public const string Disabled = "disabled";
    public const string Unconfigured = "unconfigured";
    public const string LoadFailed = "load_failed";
    public const string RuntimeFailed = "runtime_failed";
}

public sealed class LlmServiceStatus
{
    public string Code { get; init; } = LlmStatusCodes.Disabled;

    public string Message { get; init; } = string.Empty;
}

public interface ILlmStatusProvider
{
    LlmServiceStatus GetStatus();
}