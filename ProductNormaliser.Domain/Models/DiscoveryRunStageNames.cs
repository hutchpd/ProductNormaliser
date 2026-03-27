namespace ProductNormaliser.Core.Models;

public static class DiscoveryRunStageNames
{
    public const string Search = "search";
    public const string CollapseAndDedupe = "collapse_and_dedupe";
    public const string Probe = "probe";
    public const string LlmVerify = "llm_verify";
    public const string Score = "score";
    public const string Decide = "decide";
    public const string Publish = "publish";
}