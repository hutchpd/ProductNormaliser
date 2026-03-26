using System.Text;
using System.Text.RegularExpressions;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;

namespace ProductNormaliser.Infrastructure.AI;

public sealed class LlamaPageClassificationService : IPageClassificationService, ILlmStatusProvider, IDisposable
{
    private static readonly Regex YesPattern = new("\\bYES\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly LlmOptions options;
    private readonly ILogger<LlamaPageClassificationService> logger;
    private readonly Func<string, CancellationToken, Task<string>> inferenceRunner;
    private readonly bool usesCustomInferenceRunner;
    private readonly string modelBasePath;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly SemaphoreSlim inferenceLock = new(1, 1);

    private LLamaWeights? model;
    private LLamaContext? context;
    private InteractiveExecutor? executor;
    private bool initializationUnavailable;
    private bool disposed;
    private string? lastKnownStatusCode;
    private string? lastKnownStatusMessage;

    public LlamaPageClassificationService(IOptions<LlmOptions> options, ILogger<LlamaPageClassificationService> logger)
        : this(options.Value, logger)
    {
    }

    public LlamaPageClassificationService(LlmOptions options, ILogger<LlamaPageClassificationService>? logger = null)
        : this(options, RunInferenceAsyncPlaceholder, AppContext.BaseDirectory, logger, usesCustomInferenceRunner: false)
    {
        inferenceRunner = RunInferenceAsync;
    }

    public LlamaPageClassificationService(LlmOptions options, string? modelBasePath, ILogger<LlamaPageClassificationService>? logger = null)
        : this(options, RunInferenceAsyncPlaceholder, modelBasePath, logger, usesCustomInferenceRunner: false)
    {
        inferenceRunner = RunInferenceAsync;
    }

    public LlamaPageClassificationService(LlmOptions options, Func<string, CancellationToken, Task<string>> inferenceRunner, ILogger<LlamaPageClassificationService>? logger = null)
        : this(options, inferenceRunner, AppContext.BaseDirectory, logger, usesCustomInferenceRunner: true)
    {
    }

    public LlamaPageClassificationService(LlmOptions options, Func<string, CancellationToken, Task<string>> inferenceRunner, string? modelBasePath, ILogger<LlamaPageClassificationService>? logger = null)
        : this(options, inferenceRunner, modelBasePath, logger, usesCustomInferenceRunner: true)
    {
    }

    private LlamaPageClassificationService(
        LlmOptions options,
        Func<string, CancellationToken, Task<string>> inferenceRunner,
        string? modelBasePath,
        ILogger<LlamaPageClassificationService>? logger,
        bool usesCustomInferenceRunner)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        this.logger = logger ?? NullLogger<LlamaPageClassificationService>.Instance;
        ArgumentNullException.ThrowIfNull(inferenceRunner);
        this.inferenceRunner = inferenceRunner;
        this.usesCustomInferenceRunner = usesCustomInferenceRunner;
        this.modelBasePath = string.IsNullOrWhiteSpace(modelBasePath)
            ? AppContext.BaseDirectory
            : modelBasePath.Trim();
    }

    public async Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var initialStatus = GetStatus();
        if (string.Equals(initialStatus.Code, LlmStatusCodes.Disabled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(initialStatus.Code, LlmStatusCodes.Unconfigured, StringComparison.OrdinalIgnoreCase)
            || string.Equals(initialStatus.Code, LlmStatusCodes.LoadFailed, StringComparison.OrdinalIgnoreCase))
        {
            return CreateNeutralResult(MapStatusReason(initialStatus.Code), initialStatus.Code, initialStatus.Message);
        }

        try
        {
            var normalizedCategory = NormalizeCategory(category);
            var prompt = BuildPrompt(content, normalizedCategory, options.MaxContentLength);
            var inferenceTask = inferenceRunner(prompt, cancellationToken);
            var completedTask = await Task.WhenAny(inferenceTask, Task.Delay(Math.Max(1, options.TimeoutMs), cancellationToken));
            if (completedTask != inferenceTask)
            {
                logger.LogWarning("LLM page classification timed out after {TimeoutMs}ms. Continuing without LLM page classification for this request.", options.TimeoutMs);
                MarkActive("LLM validation is enabled and active.");
                return CreateNeutralResult("LLM timeout", LlmStatusCodes.Active, GetStatus().Message);
            }

            var output = await inferenceTask;
            var isYes = YesPattern.IsMatch(output ?? string.Empty);
            var confidence = isYes ? 0.8d : 0.2d;
            if (confidence < options.ConfidenceThreshold)
            {
                MarkActive("LLM validation is enabled and active.");
                return CreateNeutralResult("LLM low confidence", LlmStatusCodes.Active, GetStatus().Message);
            }

            MarkActive("LLM validation is enabled and active.");

            return new PageClassificationResult
            {
                IsProductPage = isYes,
                HasSpecifications = isYes,
                DetectedCategory = isYes ? normalizedCategory : null,
                Confidence = confidence,
                LlmStatus = LlmStatusCodes.Active,
                LlmStatusMessage = GetStatus().Message,
                Reason = isYes ? "LLM accepted representative product page." : "LLM rejected representative product page."
            };
        }
        catch (FileNotFoundException exception)
        {
            logger.LogWarning(exception, "The configured GGUF model could not be found. Continuing without LLM page classification.");
            initializationUnavailable = true;
            SetStatus(LlmStatusCodes.Unconfigured, "LLM validation is enabled, but the local GGUF model file was not found. Set Llm:ModelPath to a local model file to enable it. Discovery uses heuristics only.");
            var status = GetStatus();
            return CreateNeutralResult("LLM unconfigured", status.Code, status.Message);
        }
        catch (InvalidOperationException exception) when (string.Equals(GetStatus().Code, LlmStatusCodes.LoadFailed, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(exception, "The configured GGUF model could not be loaded. Continuing without LLM page classification.");
            var status = GetStatus();
            return CreateNeutralResult("LLM load failed", status.Code, status.Message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "LLM page classification failed. Continuing without LLM page classification for this request.");
            SetStatus(LlmStatusCodes.RuntimeFailed, "LLM validation is configured, but inference failed during this run. Discovery uses heuristics only.");
            var status = GetStatus();
            return CreateNeutralResult("LLM runtime failed", status.Code, status.Message);
        }
    }

    public LlmServiceStatus GetStatus()
    {
        ThrowIfDisposed();

        if (!options.Enabled)
        {
            return new LlmServiceStatus
            {
                Code = LlmStatusCodes.Disabled,
                Message = "LLM validation is disabled for this environment. Set Llm:Enabled=true and configure a local GGUF model to enable it. Discovery uses heuristics only."
            };
        }

        if (!string.IsNullOrWhiteSpace(lastKnownStatusCode)
            && !string.Equals(lastKnownStatusCode, LlmStatusCodes.Active, StringComparison.OrdinalIgnoreCase))
        {
            return new LlmServiceStatus
            {
                Code = lastKnownStatusCode!,
                Message = string.IsNullOrWhiteSpace(lastKnownStatusMessage)
                    ? "LLM validation needs attention. Discovery may fall back to heuristics."
                    : lastKnownStatusMessage!
            };
        }

        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            if (usesCustomInferenceRunner)
            {
                return new LlmServiceStatus
                {
                    Code = LlmStatusCodes.Active,
                    Message = "LLM validation is enabled and active."
                };
            }

            return new LlmServiceStatus
            {
                Code = LlmStatusCodes.Unconfigured,
                Message = "LLM validation is enabled, but no local GGUF model path is configured. Set Llm:ModelPath to a local model file to enable it. Discovery uses heuristics only."
            };
        }

        var modelPath = ResolveModelPath(options.ModelPath, modelBasePath);
        if (!File.Exists(modelPath))
        {
            if (usesCustomInferenceRunner)
            {
                return new LlmServiceStatus
                {
                    Code = LlmStatusCodes.Active,
                    Message = "LLM validation is enabled and active."
                };
            }

            return new LlmServiceStatus
            {
                Code = LlmStatusCodes.Unconfigured,
                Message = "LLM validation is enabled, but the local GGUF model file was not found. Set Llm:ModelPath to a local model file to enable it. Discovery uses heuristics only."
            };
        }

        return new LlmServiceStatus
        {
            Code = LlmStatusCodes.Active,
            Message = executor is null
                ? "LLM validation is enabled and ready to initialize on first use."
                : "LLM validation is enabled and active."
        };
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
            var status = GetStatus();
            if (string.Equals(status.Code, LlmStatusCodes.Unconfigured, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException(status.Message, ResolveModelPath(options.ModelPath, modelBasePath));
            }

            throw new InvalidOperationException(status.Message);
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

            var modelPath = ResolveModelPath(options.ModelPath, modelBasePath);
            if (!File.Exists(modelPath))
            {
                initializationUnavailable = true;
                SetStatus(LlmStatusCodes.Unconfigured, "LLM validation is enabled, but the local GGUF model file was not found. Set Llm:ModelPath to a local model file to enable it. Discovery uses heuristics only.");
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
            MarkActive("LLM validation is enabled and active.");
            return true;
        }
        catch (Exception exception)
        {
            initializationUnavailable = true;
            SetStatus(LlmStatusCodes.LoadFailed, "LLM validation is enabled, but the local model failed to load. Verify the GGUF file and runtime dependencies, then try again. Discovery uses heuristics only.");
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

    private static string ResolveModelPath(string? configuredPath, string? basePath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? "models/tinyllama.gguf"
            : configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, string.IsNullOrWhiteSpace(basePath) ? AppContext.BaseDirectory : basePath);
    }

    private static Task<string> RunInferenceAsyncPlaceholder(string _, CancellationToken __)
    {
        throw new NotSupportedException("This placeholder should be replaced by the instance RunInferenceAsync method.");
    }

    private static PageClassificationResult CreateNeutralResult(string reason)
    {
        return new PageClassificationResult
        {
            IsProductPage = false,
            HasSpecifications = false,
            Confidence = 0d,
            LlmStatus = LlmStatusCodes.Active,
            Reason = reason
        };
    }

    private static PageClassificationResult CreateNeutralResult(string reason, string statusCode, string? statusMessage)
    {
        return new PageClassificationResult
        {
            IsProductPage = false,
            HasSpecifications = false,
            Confidence = 0d,
            LlmStatus = statusCode,
            LlmStatusMessage = statusMessage,
            Reason = reason
        };
    }

    private static string MapStatusReason(string statusCode)
    {
        return statusCode switch
        {
            LlmStatusCodes.Disabled => "LLM disabled",
            LlmStatusCodes.Unconfigured => "LLM unconfigured",
            LlmStatusCodes.LoadFailed => "LLM load failed",
            LlmStatusCodes.RuntimeFailed => "LLM runtime failed",
            _ => "LLM disabled"
        };
    }

    private void MarkActive(string message)
    {
        SetStatus(LlmStatusCodes.Active, message);
    }

    private void SetStatus(string code, string message)
    {
        lastKnownStatusCode = code;
        lastKnownStatusMessage = message;
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