namespace ProductNormaliser.Infrastructure.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public bool Enabled { get; set; } = true;

    public int MaxContentLength { get; set; } = 4000;

    public double ConfidenceThreshold { get; set; } = 0.7d;

    public string ModelPath { get; set; } = "models/tinyllama.gguf";

    public uint ContextSize { get; set; } = 2048;

    public int MaxTokens { get; set; } = 8;
}