using System.Text;
using System.Text.RegularExpressions;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;

namespace ProductNormaliser.Infrastructure.AI;

public sealed class LlamaPageClassificationService : IPageClassificationService, IDisposable
{
    private static readonly Regex YesPattern = new("\\bYES\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly LlmOptions options;
    private readonly ILogger<LlamaPageClassificationService> logger;
    private readonly Func<string, CancellationToken, Task<string>> inferenceRunner;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly SemaphoreSlim inferenceLock = new(1, 1);

    private LLamaWeights? model;
    private LLamaContext? context;
    private InteractiveExecutor? executor;
    private bool initializationUnavailable;
    private bool disposed;

    public LlamaPageClassificationService(IOptions<LlmOptions> options, ILogger<LlamaPageClassificationService> logger)
        : this(options.Value, logger)
    {
    }

    public LlamaPageClassificationService(LlmOptions options, ILogger<LlamaPageClassificationService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        this.logger = logger ?? NullLogger<LlamaPageClassificationService>.Instance;
        inferenceRunner = RunInferenceAsync;
    }

    public LlamaPageClassificationService(LlmOptions options, Func<string, CancellationToken, Task<string>> inferenceRunner, ILogger<LlamaPageClassificationService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(inferenceRunner);

        this.options = options;
        this.logger = logger ?? NullLogger<LlamaPageClassificationService>.Instance;
        this.inferenceRunner = inferenceRunner;
    }

    public async Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!options.Enabled)
        {
            return CreateNeutralResult();
        }

        try
        {
            var normalizedCategory = NormalizeCategory(category);
            var prompt = BuildPrompt(content, normalizedCategory, options.MaxContentLength);
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
        catch (FileNotFoundException exception)
        {
            logger.LogWarning(exception, "The configured GGUF model could not be found. Continuing without LLM page classification.");
            initializationUnavailable = true;
            return CreateNeutralResult();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "LLM page classification failed. Continuing without LLM page classification for this request.");
            return CreateNeutralResult();
        }
    }

    internal static string BuildPrompt(string content, string category, int maxContentLength)
    {
        var normalizedCategory = NormalizeCategory(category);
        var truncatedContent = TruncateContent(content, maxContentLength);

        return $"""
You are validating a webpage.

Question:
Is this a product page for {normalizedCategory} with technical specifications?

Answer only YES or NO.

Content:
{truncatedContent}
""";
    }

    internal static string TruncateContent(string? content, int maxContentLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var boundedLength = Math.Max(1, maxContentLength);

        return content.Length <= boundedLength
            ? content
            : content[..boundedLength];
    }

    private async Task<string> RunInferenceAsync(string prompt, CancellationToken cancellationToken)
    {
        if (!await EnsureExecutorAsync(cancellationToken))
        {
            throw new FileNotFoundException("The configured GGUF model could not be loaded.", ResolveModelPath(options.ModelPath));
        }

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

    private async Task<bool> EnsureExecutorAsync(CancellationToken cancellationToken)
    {
        if (executor is not null)
        {
            return true;
        }

        if (initializationUnavailable)
        {
            return false;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (executor is not null)
            {
                return true;
            }

            if (initializationUnavailable)
            {
                return false;
            }

            var modelPath = ResolveModelPath(options.ModelPath);
            if (!File.Exists(modelPath))
            {
                initializationUnavailable = true;
                logger.LogWarning("The configured GGUF model could not be found at {ModelPath}. Continuing without LLM page classification.", modelPath);
                return false;
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = Math.Max(512u, options.ContextSize)
            };

            model = LLamaWeights.LoadFromFile(parameters);
            context = model.CreateContext(parameters);
            executor = new InteractiveExecutor(context);
            return true;
        }
        catch (Exception exception)
        {
            initializationUnavailable = true;
            logger.LogWarning(exception, "Failed to initialize the GGUF model at {ModelPath}. Continuing without LLM page classification.", options.ModelPath);
            return false;
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

    private static PageClassificationResult CreateNeutralResult()
    {
        return new PageClassificationResult
        {
            IsProductPage = false,
            HasSpecifications = false,
            Confidence = 0d
        };
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