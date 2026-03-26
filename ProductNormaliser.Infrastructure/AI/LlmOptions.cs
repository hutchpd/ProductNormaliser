namespace ProductNormaliser.Infrastructure.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string ModelPath { get; set; } = "models/tinyllama.gguf";

    public uint ContextSize { get; set; } = 2048;

    public int MaxTokens { get; set; } = 8;
}