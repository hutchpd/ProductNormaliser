using System.Text;
using System.Text.RegularExpressions;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;

namespace ProductNormaliser.Infrastructure.AI;

public sealed class LlamaPageClassificationService : IPageClassificationService, IDisposable
{
    private const int MaxPromptContentLength = 4000;
    private static readonly Regex YesPattern = new("\\bYES\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly LlmOptions options;
    private readonly Func<string, CancellationToken, Task<string>> inferenceRunner;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly SemaphoreSlim inferenceLock = new(1, 1);

    private LLamaWeights? model;
    private LLamaContext? context;
    private InteractiveExecutor? executor;
    private bool disposed;

    public LlamaPageClassificationService(IOptions<LlmOptions> options)
        : this(options.Value)
    {
    }

    public LlamaPageClassificationService(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        inferenceRunner = RunInferenceAsync;
    }

    public LlamaPageClassificationService(LlmOptions options, Func<string, CancellationToken, Task<string>> inferenceRunner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(inferenceRunner);

        this.options = options;
        this.inferenceRunner = inferenceRunner;
    }

    public async Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var normalizedCategory = NormalizeCategory(category);
        var prompt = BuildPrompt(content, normalizedCategory);
        var output = await inferenceRunner(prompt, cancellationToken);
        var isYes = YesPattern.IsMatch(output ?? string.Empty);

        return new PageClassificationResult
        {
            IsProductPage = isYes,
            HasSpecifications = isYes,
            DetectedCategory = isYes ? normalizedCategory : null,
            Confidence = isYes ? 0.8d : 0.2d
        };
    }

    internal static string BuildPrompt(string content, string category)
    {
        var normalizedCategory = NormalizeCategory(category);
        var truncatedContent = TruncateContent(content);

        return $"""
You are validating a webpage.

Question:
Is this a product page for {normalizedCategory} with technical specifications?

Answer only YES or NO.

Content:
{truncatedContent}
""";
    }

    internal static string TruncateContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return content.Length <= MaxPromptContentLength
            ? content
            : content[..MaxPromptContentLength];
    }

    private async Task<string> RunInferenceAsync(string prompt, CancellationToken cancellationToken)
    {
        await EnsureExecutorAsync(cancellationToken);

        await inferenceLock.WaitAsync(cancellationToken);
        try
        {
            var output = new StringBuilder();
            var inferenceParams = new InferenceParams
            {
                MaxTokens = Math.Max(1, options.MaxTokens)
            };

            await foreach (var token in executor!.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                output.Append(token);
            }

            return output.ToString().Trim();
        }
        finally
        {
            inferenceLock.Release();
        }
    }

    private async Task EnsureExecutorAsync(CancellationToken cancellationToken)
    {
        if (executor is not null)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (executor is not null)
            {
                return;
            }

            var modelPath = ResolveModelPath(options.ModelPath);
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"The configured GGUF model could not be found at '{modelPath}'.", modelPath);
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = Math.Max(512u, options.ContextSize)
            };

            model = LLamaWeights.LoadFromFile(parameters);
            context = model.CreateContext(parameters);
            executor = new InteractiveExecutor(context);
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private static string NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? "the requested category"
            : category.Trim();
    }

    private static string ResolveModelPath(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? "models/tinyllama.gguf"
            : configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, AppContext.BaseDirectory);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        executor = null;
        context?.Dispose();
        model?.Dispose();
        initializationLock.Dispose();
        inferenceLock.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}