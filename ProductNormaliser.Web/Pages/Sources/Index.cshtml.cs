using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ProductNormaliser.Web.Pages.Sources;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    private const string OperatorAssistedMode = "operator_assisted";
    private const string SuggestAcceptMode = "suggest_accept";
    private const string AutoAcceptAndSeedMode = "auto_accept_and_seed";
    private const int DiscoveryRunHistoryPageSize = 10;
    private const int ExpandedDiscoveryRunCount = 3;

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "enabled")]
    public bool? Enabled { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public RegisterSourceInput Registration { get; set; } = new();

    [BindProperty]
    public DiscoverSourceCandidatesInput CandidateDiscovery { get; set; } = new();

    [BindProperty]
    public UseCandidateInput CandidateSelection { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public string? CandidateDiscoveryErrorMessage { get; private set; }

    public SourceCandidateDiscoveryResponseDto? CandidateDiscoveryResult { get; private set; }

    public SourceOnboardingAutomationSettingsDto AutomationSettings { get; private set; } = new()
    {
        DefaultMode = OperatorAssistedMode,
        LlmStatus = "disabled",
        LlmStatusMessage = "LLM validation is disabled for this environment. Set Llm:Enabled=true and configure a local GGUF model to enable it. Discovery uses heuristics only.",
        AutomationCategorySampleBudget = 3,
        AutomationProductSampleBudget = 3,
        SuggestMinReachableCategorySamples = 2,
        SuggestMinReachableProductSamples = 2,
        SuggestMinRuntimeCompatibleProductSamples = 2,
        AutoAcceptMinReachableCategorySamples = 3,
        AutoAcceptMinReachableProductSamples = 3,
        AutoAcceptMinRuntimeCompatibleProductSamples = 3,
        AutoAcceptMinStructuredEvidenceProductSamples = 2,
        MaxAutoAcceptedCandidatesPerRun = 1
    };

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SourceDto> AllSources { get; private set; } = [];

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public IReadOnlyList<DiscoveryRunDto> ExpandedDiscoveryRuns { get; private set; } = [];

    public IReadOnlyList<DiscoveryRunDto> CollapsedDiscoveryRuns { get; private set; } = [];

    public bool HasCollapsedDiscoveryRuns => CollapsedDiscoveryRuns.Count > 0;

    public int TotalSources { get; private set; }

    public int ReadySources => Sources.Count(source => string.Equals(source.Readiness.Status, "Ready", StringComparison.OrdinalIgnoreCase));

    public int AttentionSources => Sources.Count(source => string.Equals(source.Health.Status, "Attention", StringComparison.OrdinalIgnoreCase));

    public int DiscoveryBacklogSources => Sources.Count(source => source.DiscoveryQueueDepth > 0);

    public int ActiveDiscoverySources => Sources.Count(source => source.LastDiscoveryUtc is not null || source.ConfirmedProductUrlsLast24Hours > 0);

    public int RegistryEnabledSources => AllSources.Count(source => source.IsEnabled);

    public int RegistryDiscoveryConfiguredSources => AllSources.Count(HasDiscoveryScaffold);

    public int RegistryBootReadySources => AllSources.Count(IsBootReady);

    public int ActiveDiscoveryRunCount => ExpandedDiscoveryRuns.Concat(CollapsedDiscoveryRuns)
        .Count(run => DiscoveryRunPresentation.IsActiveStatus(run.Status));

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Source Registry",
        Title = "Manage enabled hosts and source readiness",
        Description = "Review each crawl host’s assigned category coverage, throttling posture, readiness, health indicators, and recent crawl activity before changing source state.",
        Metrics =
        [
            new HeroMetricModel { Label = "Filtered", Value = Sources.Count.ToString() },
            new HeroMetricModel { Label = "Total", Value = TotalSources.ToString() },
            new HeroMetricModel { Label = "Enabled", Value = Sources.Count(source => source.IsEnabled).ToString() },
            new HeroMetricModel { Label = "Discovery active", Value = ActiveDiscoverySources.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        return await RegisterAsync(Registration, acceptedFromCandidate: false, cancellationToken);
    }

    public async Task<IActionResult> OnPostDiscoverCandidatesAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        await LoadAsync(cancellationToken);

        var categoryKeys = CandidateDiscovery.CategoryKeys;
        if (categoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
        {
            categoryKeys = [CategoryKey.Trim()];
            CandidateDiscovery.CategoryKeys = categoryKeys;
        }

        try
        {
            var run = await adminApiClient.CreateDiscoveryRunAsync(new CreateDiscoveryRunRequest
            {
                CategoryKeys = categoryKeys,
                Locale = string.IsNullOrWhiteSpace(CandidateDiscovery.Locale) ? null : CandidateDiscovery.Locale.Trim(),
                Market = string.IsNullOrWhiteSpace(CandidateDiscovery.Market) ? null : CandidateDiscovery.Market.Trim(),
                AutomationMode = CandidateDiscovery.AutomationMode,
                BrandHints = ParseDelimitedValues(CandidateDiscovery.BrandHints),
                MaxCandidates = CandidateDiscovery.MaxCandidates
            }, cancellationToken);

            StatusMessage = $"Started discovery run '{run.RunId}'. The details page will keep polling while background work progresses.";
            return RedirectToPage("/Sources/DiscoveryRuns/Details", new { runId = run.RunId });
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }

            return Page();
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to create discovery run from sources index.");
            CandidateDiscoveryErrorMessage = IsCandidateDiscoveryTimeout(exception)
                ? "Candidate discovery took too long to complete. Try fewer categories or use operator-assisted mode, then retry. Manual source registration remains available."
                : exception.Message;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUseCandidateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: false, cancellationToken);

        ApplyCandidateToRegistration(CandidateSelection);
        StatusMessage = $"Prefilled registration from candidate '{Registration.DisplayName}'. Review and submit to register the source.";

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptCandidateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: false, cancellationToken);

        ApplyCandidateToRegistration(CandidateSelection);
        return await RegisterAsync(Registration, acceptedFromCandidate: true, cancellationToken);
    }

    public async Task<IActionResult> OnPostDismissCandidateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: false, cancellationToken);

        AddDismissedCandidate(CandidateSelection.CandidateKey);
        StatusMessage = $"Dismissed candidate '{CandidateSelection.DisplayName}'. It has been moved into this run's archive.";

        return Page();
    }

    public async Task<IActionResult> OnPostRestoreCandidateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: false, cancellationToken);

        RemoveDismissedCandidate(CandidateSelection.CandidateKey);
        StatusMessage = $"Restored candidate '{CandidateSelection.DisplayName}' to the active queue.";

        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(string sourceId, bool currentlyEnabled, string? category, string? search, bool? enabled, CancellationToken cancellationToken)
    {
        CategoryKey = category;
        Search = search;
        Enabled = enabled;

        try
        {
            if (currentlyEnabled)
            {
                await adminApiClient.DisableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Disabled source '{sourceId}'.";
            }
            else
            {
                await adminApiClient.EnableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Enabled source '{sourceId}'.";
            }

            return RedirectToPage(new { category = CategoryKey, search = Search, enabled = Enabled });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to toggle source {SourceId} from sources index.", sourceId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public IReadOnlyList<string> GetAssignedCategoryLabels(SourceDto source)
    {
        if (source.SupportedCategoryKeys.Count == 0)
        {
            return ["No categories assigned"];
        }

        var categoryLookup = Categories.ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);
        return source.SupportedCategoryKeys
            .Select(categoryKey => categoryLookup.TryGetValue(categoryKey, out var category) ? category.DisplayName : categoryKey)
            .ToArray();
    }

    public string BuildIntelligenceUrl(SourceDto source)
    {
        var categoryKey = !string.IsNullOrWhiteSpace(CategoryKey)
            ? CategoryKey
            : source.SupportedCategoryKeys.FirstOrDefault();

        return string.IsNullOrWhiteSpace(categoryKey)
            ? "/Sources/Intelligence"
            : $"/Sources/Intelligence?category={Uri.EscapeDataString(categoryKey)}&source={Uri.EscapeDataString(source.DisplayName)}";
    }

    public bool HasDiscoveryScaffold(SourceDto source)
    {
        return source.DiscoveryProfile.CategoryEntryPages.Count > 0
            || source.DiscoveryProfile.SitemapHints.Count > 0
            || source.DiscoveryProfile.ProductUrlPatterns.Count > 0
            || source.DiscoveryProfile.ListingUrlPatterns.Count > 0;
    }

    public bool IsBootReady(SourceDto source)
    {
        return source.IsEnabled
            && HasDiscoveryScaffold(source)
            && source.Readiness.CrawlableCategoryCount > 0;
    }

    public string GetSetupSummary(SourceDto source)
    {
        if (!source.IsEnabled)
        {
            return "Disabled until explicitly enabled.";
        }

        if (!HasDiscoveryScaffold(source))
        {
            return "No discovery seed profile is configured yet.";
        }

        if (source.Readiness.CrawlableCategoryCount == 0)
        {
            return "Assigned categories are not crawl-ready yet.";
        }

        return "Ready to seed discovery from startup defaults or an edited profile.";
    }

    public string GetCandidateRecommendationLabel(SourceCandidateDto candidate)
    {
        return candidate.RecommendationStatus switch
        {
            "recommended" => "Recommended",
            "do_not_accept" => "Do not accept",
            _ => "Manual review"
        };
    }

    public string GetCandidateRecommendationTone(SourceCandidateDto candidate)
    {
        return candidate.RecommendationStatus switch
        {
            "recommended" => "completed",
            "do_not_accept" => "warning",
            _ => "pending"
        };
    }

    public bool CanAcceptCandidate(SourceCandidateDto candidate)
    {
        return !candidate.AlreadyRegistered
            && candidate.AllowedByGovernance
            && string.Equals(candidate.RecommendationStatus, "recommended", StringComparison.OrdinalIgnoreCase);
    }

    public string GetAutomationModeLabel(string? mode)
    {
        return NormalizeAutomationMode(mode) switch
        {
            SuggestAcceptMode => "Suggest accept",
            AutoAcceptAndSeedMode => "Auto-accept and seed",
            _ => "Operator-assisted"
        };
    }

    public string GetAutomationDecisionLabel(SourceCandidateDto candidate)
    {
        return candidate.AutomationAssessment.Decision switch
        {
            "auto_accept_and_seed" => "Auto-ready",
            "suggest_accept" => "Safe to accept",
            _ => "Manual only"
        };
    }

    public string GetAutomationDecisionTone(SourceCandidateDto candidate)
    {
        return candidate.AutomationAssessment.Decision switch
        {
            "auto_accept_and_seed" => "completed",
            "suggest_accept" => "pending",
            _ => "warning"
        };
    }

    public string GetRuntimeExtractionLabel(SourceCandidateDto candidate)
    {
        return candidate.RuntimeExtractionStatus switch
        {
            "compatible" => "Compatible with runtime extraction",
            "not_compatible" => "Not compatible with runtime extraction",
            _ => "Manual review only"
        };
    }

    public string GetRuntimeExtractionTone(SourceCandidateDto candidate)
    {
        return candidate.RuntimeExtractionStatus switch
        {
            "compatible" => "completed",
            "not_compatible" => "warning",
            _ => "pending"
        };
    }

    public string GetDiscoveryDiagnosticNoticeClass(SourceCandidateDiscoveryDiagnosticDto diagnostic)
    {
        return diagnostic.Severity switch
        {
            "error" => "notice error",
            "warning" => "notice warning",
            _ => "notice info"
        };
    }

    public string GetCandidateDiscoveryEmptyStateHeading()
    {
        if (CandidateDiscoveryResult?.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "Candidate discovery is currently degraded.";
        }

        return "No candidate hosts matched this discovery request.";
    }

    public string GetCandidateDiscoveryEmptyStateMessage()
    {
        if (CandidateDiscoveryResult?.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "Review the diagnostics above, then retry discovery once provider configuration or upstream availability has been restored. Manual registration remains available in the meantime.";
        }

        return "Try broadening the category scope, reducing brand hints, or using a different market or locale. Registered sources remain unchanged.";
    }

    public string GetLlmStatusTitle(string? status)
    {
        return NormalizeLlmStatus(status) switch
        {
            "active" => "LLM validation active",
            "unconfigured" => "LLM not configured locally",
            "load_failed" => "LLM model failed to load",
            "runtime_failed" => "LLM validation failed during discovery",
            _ => "LLM validation disabled"
        };
    }

    public string GetLlmStatusNoticeClass(string? status)
    {
        return NormalizeLlmStatus(status) switch
        {
            "active" => "notice success",
            "unconfigured" => "notice warning",
            "load_failed" => "notice warning",
            "runtime_failed" => "notice warning",
            _ => "notice info"
        };
    }

    public bool HasAutomationLlmStatusNotice()
    {
        return !string.IsNullOrWhiteSpace(AutomationSettings.LlmStatus)
            && !string.IsNullOrWhiteSpace(AutomationSettings.LlmStatusMessage);
    }

    public bool HasDiscoveryLlmStatusNotice()
    {
        return CandidateDiscoveryResult is not null
            && !string.IsNullOrWhiteSpace(CandidateDiscoveryResult.LlmStatus)
            && !string.IsNullOrWhiteSpace(CandidateDiscoveryResult.LlmStatusMessage);
    }

    public IReadOnlyList<SourceCandidateDto> GetVisibleCandidateResults()
    {
        return CandidateDiscoveryResult?.Candidates
            .Where(candidate => !IsDismissedCandidate(candidate.CandidateKey))
            .ToArray() ?? [];
    }

    public IReadOnlyList<SourceCandidateDto> GetArchivedCandidateResults()
    {
        return CandidateDiscoveryResult?.Candidates
            .Where(candidate => IsDismissedCandidate(candidate.CandidateKey))
            .ToArray() ?? [];
    }

    public string GetCandidateProbeTimingSummary(SourceCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.Probe.ProbeElapsedMs <= 0)
        {
            return candidate.Probe.ProbeAttemptCount > 1
                ? $"Probe retried {candidate.Probe.ProbeAttemptCount} times."
                : string.Empty;
        }

        return candidate.Probe.ProbeAttemptCount > 1
            ? $"Probe took {FormatDuration(candidate.Probe.ProbeElapsedMs)} across {candidate.Probe.ProbeAttemptCount} attempt(s)."
            : $"Probe took {FormatDuration(candidate.Probe.ProbeElapsedMs)}.";
    }

    public string GetCandidateLlmTimingSummary(SourceCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Probe.LlmElapsedMs is > 0
            ? $"Local LLM verification took {FormatDuration(candidate.Probe.LlmElapsedMs.Value)}."
            : string.Empty;
    }

    public IReadOnlyList<string> GetCandidatePrimaryReasons(SourceCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Reasons
            .Select(reason => reason.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Take(3)
            .ToArray();
    }

    public IReadOnlyList<string> GetCandidatePrimaryBlockers(SourceCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.AutomationAssessment.BlockingReasons
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Take(3)
            .ToArray();
    }

    public IReadOnlyList<string> GetCandidateVisibleSitemapHints(SourceCandidateDto candidate, int maxCount = 6)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Probe.SitemapUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Take(Math.Max(1, maxCount))
            .ToArray();
    }

    public int GetCandidateHiddenSitemapHintCount(SourceCandidateDto candidate, int maxCount = 6)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return Math.Max(0, candidate.Probe.SitemapUrls.Count - Math.Max(1, maxCount));
    }

    public bool HasCandidateExpandedDetail(SourceCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.AutomationAssessment.SupportingReasons.Count > 0
            || candidate.AutomationAssessment.BlockingReasons.Count > 0
            || candidate.Probe.RepresentativeCategoryPageReachable
            || candidate.Probe.RepresentativeProductPageReachable
            || candidate.Probe.StructuredProductEvidenceDetected
            || candidate.Probe.TechnicalAttributeEvidenceDetected
            || candidate.Reasons.Count > 3
            || candidate.Probe.LikelyListingUrlPatterns.Count > 0
            || candidate.Probe.LikelyProductUrlPatterns.Count > 0
            || candidate.Probe.SitemapUrls.Count > 0;
    }

    public string GetDiscoveryRunScopeSummary(DiscoveryRunDto run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var summary = new List<string>();
        if (run.RequestedCategoryKeys.Count > 0)
        {
            summary.Add(string.Join(", ", run.RequestedCategoryKeys));
        }

        if (!string.IsNullOrWhiteSpace(run.Market))
        {
            summary.Add($"market {run.Market}");
        }

        if (!string.IsNullOrWhiteSpace(run.Locale))
        {
            summary.Add($"locale {run.Locale}");
        }

        if (run.BrandHints.Count > 0)
        {
            summary.Add($"brands {string.Join(", ", run.BrandHints)}");
        }

        return summary.Count == 0 ? "No explicit scope" : string.Join(" | ", summary);
    }

    public string GetDiscoveryRunTimingSummary(DiscoveryRunDto run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var parts = new List<string>();
        if (run.SearchElapsedMs is not null)
        {
            parts.Add($"search {run.SearchElapsedMs.Value} ms");
        }

        if (run.ProbeAverageElapsedMs is not null)
        {
            parts.Add($"probe avg {run.ProbeAverageElapsedMs.Value} ms");
        }

        if (run.LlmAverageElapsedMs is not null)
        {
            parts.Add($"LLM avg {run.LlmAverageElapsedMs.Value} ms");
        }

        if (run.TimeToFirstAcceptedCandidateMs is not null)
        {
            parts.Add($"first accept {run.TimeToFirstAcceptedCandidateMs.Value} ms");
        }

        return string.Join(" | ", parts);
    }

    public string GetDiscoveryRunOutcomeSummary(DiscoveryRunDto run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var parts = new List<string>
        {
            $"{run.PublishedCandidateCount} published",
            $"{run.SuggestedCandidateCount} manual review",
            $"{run.LlmQueueDepth} queued for LLM"
        };

        if (run.AcceptanceRate is not null)
        {
            parts.Add($"acceptance {run.AcceptanceRate.Value:0.#}%");
        }

        if (run.CandidateThroughputPerMinute is not null)
        {
            parts.Add($"throughput {run.CandidateThroughputPerMinute.Value:0.#}/min");
        }

        return string.Join(" | ", parts);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);
            var automationSettingsTask = adminApiClient.GetSourceOnboardingAutomationSettingsAsync(cancellationToken);
            var discoveryRunsTask = adminApiClient.GetDiscoveryRunsAsync(pageSize: DiscoveryRunHistoryPageSize, cancellationToken: cancellationToken);
            await Task.WhenAll(categoriesTask, sourcesTask, automationSettingsTask, discoveryRunsTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            AutomationSettings = automationSettingsTask.Result;
            ApplyDiscoveryRunHistory(discoveryRunsTask.Result.Items);
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                CategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            CategoryKey = categoryContext.PrimaryCategoryKey;

            var allSources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            AllSources = allSources;
            TotalSources = allSources.Length;

            IEnumerable<SourceDto> filtered = allSources;
            if (!string.IsNullOrWhiteSpace(CategoryKey))
            {
                filtered = filtered.Where(source => source.SupportedCategoryKeys.Contains(CategoryKey, StringComparer.OrdinalIgnoreCase));
            }

            if (Enabled.HasValue)
            {
                filtered = filtered.Where(source => source.IsEnabled == Enabled.Value);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                filtered = filtered.Where(source =>
                    source.SourceId.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || source.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || source.Host.Contains(Search, StringComparison.OrdinalIgnoreCase));
            }

            Sources = filtered.ToArray();

            if (Registration.CategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
            {
                Registration.CategoryKeys = [CategoryKey];
            }

            Registration.AutomationMode = NormalizeAutomationMode(string.IsNullOrWhiteSpace(Registration.AutomationMode) ? AutomationSettings.DefaultMode : Registration.AutomationMode);

            if (CandidateDiscovery.CategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
            {
                CandidateDiscovery.CategoryKeys = [CategoryKey];
            }

            CandidateDiscovery.AutomationMode = NormalizeAutomationMode(string.IsNullOrWhiteSpace(CandidateDiscovery.AutomationMode) ? AutomationSettings.DefaultMode : CandidateDiscovery.AutomationMode);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load sources page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            AllSources = [];
            Sources = [];
            ExpandedDiscoveryRuns = [];
            CollapsedDiscoveryRuns = [];
            AutomationSettings = new SourceOnboardingAutomationSettingsDto
            {
                DefaultMode = OperatorAssistedMode,
                LlmStatus = "disabled",
                LlmStatusMessage = "LLM validation is disabled for this environment. Set Llm:Enabled=true and configure a local GGUF model to enable it. Discovery uses heuristics only.",
                AutomationCategorySampleBudget = 3,
                AutomationProductSampleBudget = 3,
                SuggestMinReachableCategorySamples = 2,
                SuggestMinReachableProductSamples = 2,
                SuggestMinRuntimeCompatibleProductSamples = 2,
                AutoAcceptMinReachableCategorySamples = 3,
                AutoAcceptMinReachableProductSamples = 3,
                AutoAcceptMinRuntimeCompatibleProductSamples = 3,
                AutoAcceptMinStructuredEvidenceProductSamples = 2,
                MaxAutoAcceptedCandidatesPerRun = 1
            };
        }
    }

    private void ApplyDiscoveryRunHistory(IReadOnlyList<DiscoveryRunDto> runs)
    {
        var orderedRuns = runs
            .OrderByDescending(run => run.CreatedUtc)
            .ToArray();

        ExpandedDiscoveryRuns = orderedRuns
            .Take(ExpandedDiscoveryRunCount)
            .ToArray();

        CollapsedDiscoveryRuns = orderedRuns
            .Skip(ExpandedDiscoveryRunCount)
            .ToArray();
    }

    private static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseDelimitedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return NormalizeValues(value
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task DiscoverCandidatesAsync(bool addValidationErrorWhenEmpty, CancellationToken cancellationToken)
    {
        var categoryKeys = NormalizeValues(CandidateDiscovery.CategoryKeys);
        if (categoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
        {
            categoryKeys = [CategoryKey];
        }

        if (categoryKeys.Count == 0)
        {
            if (addValidationErrorWhenEmpty)
            {
                ModelState.AddModelError($"{nameof(CandidateDiscovery)}.{nameof(CandidateDiscovery.CategoryKeys)}", "Choose at least one category before discovering source candidates.");
            }

            CandidateDiscoveryResult = null;
            return;
        }

        CandidateDiscovery.CategoryKeys = categoryKeys;

        try
        {
            CandidateDiscoveryErrorMessage = null;
            CandidateDiscoveryResult = null;
            CandidateDiscoveryResult = await adminApiClient.DiscoverSourceCandidatesAsync(new DiscoverSourceCandidatesRequest
            {
                CategoryKeys = categoryKeys,
                Locale = NormalizeOptionalText(CandidateDiscovery.Locale),
                Market = NormalizeOptionalText(CandidateDiscovery.Market),
                AutomationMode = NormalizeAutomationMode(CandidateDiscovery.AutomationMode),
                BrandHints = ParseDelimitedValues(CandidateDiscovery.BrandHints),
                MaxCandidates = NormalizeMaxCandidates(CandidateDiscovery.MaxCandidates)
            }, cancellationToken);
            ApplyDismissedCandidates();
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to discover source candidates from sources index.");
            CandidateDiscoveryErrorMessage = IsCandidateDiscoveryTimeout(exception)
                ? "Candidate discovery took too long to complete. Try fewer categories or use operator-assisted mode, then retry. Manual source registration remains available."
                : exception.Message;
        }
    }

    private static bool IsCandidateDiscoveryTimeout(AdminApiException exception)
    {
        if (exception.InnerException is TaskCanceledException)
        {
            return true;
        }

        return exception.Message.Contains("HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("operation was canceled", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("request was canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLlmStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "disabled"
            : status.Trim().ToLowerInvariant();
    }

    private async Task<IActionResult> RegisterAsync(RegisterSourceInput registration, bool acceptedFromCandidate, CancellationToken cancellationToken)
    {
        try
        {
            var source = await adminApiClient.RegisterSourceAsync(BuildRegisterRequest(registration), cancellationToken);

            StatusMessage = acceptedFromCandidate
                ? $"Accepted candidate '{source.DisplayName}' and registered it as a managed source. Startup discovery defaults were applied automatically."
                : $"Registered source '{source.DisplayName}'. Startup discovery defaults were applied automatically.";
            return RedirectToPage("/Sources/Details", new { sourceId = source.SourceId });
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, acceptedFromCandidate
                ? "Failed to accept candidate from sources index."
                : "Failed to register source from sources index.");
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private void ApplyCandidateToRegistration(UseCandidateInput candidate)
    {
        var categoryKeys = NormalizeValues(candidate.CategoryKeys);
        if (categoryKeys.Count == 0)
        {
            categoryKeys = NormalizeValues(CandidateDiscovery.CategoryKeys);
        }

        Registration = new RegisterSourceInput
        {
            SourceId = DeriveSourceId(candidate),
            DisplayName = candidate.DisplayName.Trim(),
            BaseUrl = NormalizeBaseUrl(candidate.BaseUrl),
            Description = Registration.Description,
            IsEnabled = candidate.IsEnabled,
            AllowedMarkets = ResolveCandidateAllowedMarkets(candidate),
            PreferredLocale = NormalizeOptionalText(candidate.PreferredLocale) ?? CandidateDiscovery.Locale ?? "en-GB",
            AutomationMode = NormalizeAutomationMode(Registration.AutomationMode),
            CategoryKeys = categoryKeys
        };
    }

    private async Task ApplyGuardedAutomationAsync(CancellationToken cancellationToken)
    {
        if (CandidateDiscoveryResult is null
            || NormalizeAutomationMode(CandidateDiscovery.AutomationMode) != AutoAcceptAndSeedMode)
        {
            return;
        }

        var maxAutoAccepted = Math.Max(0, AutomationSettings.MaxAutoAcceptedCandidatesPerRun);
        if (maxAutoAccepted == 0)
        {
            return;
        }

        var candidatesToAccept = CandidateDiscoveryResult.Candidates
            .Where(candidate => candidate.AutomationAssessment.EligibleForAutoAccept)
            .Take(maxAutoAccepted)
            .ToArray();

        if (candidatesToAccept.Length == 0)
        {
            StatusMessage = "Guarded automation reviewed the current candidates, but none met the auto-accept and auto-seed guardrails.";
            return;
        }

        var acceptedCandidateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acceptedSources = new List<string>();
        var seededJobIds = new List<string>();

        foreach (var candidate in candidatesToAccept)
        {
            var registration = BuildRegistrationFromCandidate(candidate, AutoAcceptAndSeedMode);

            try
            {
                var source = await adminApiClient.RegisterSourceAsync(BuildRegisterRequest(registration), cancellationToken);
                acceptedCandidateKeys.Add(candidate.CandidateKey);
                acceptedSources.Add(source.DisplayName);

                if (candidate.AutomationAssessment.EligibleForAutoSeed && registration.CategoryKeys.Count > 0)
                {
                    var job = await adminApiClient.CreateCrawlJobAsync(new CreateCrawlJobRequest
                    {
                        RequestType = "category",
                        RequestedCategories = registration.CategoryKeys,
                        RequestedSources = [source.SourceId]
                    }, cancellationToken);
                    seededJobIds.Add(job.JobId);
                }
            }
            catch (AdminApiValidationException exception)
            {
                foreach (var entry in exception.Errors)
                {
                    foreach (var message in entry.Value)
                    {
                        ModelState.AddModelError(string.Empty, $"Guarded automation skipped '{candidate.DisplayName}': {message}");
                    }
                }
            }
            catch (AdminApiException exception)
            {
                logger.LogWarning(exception, "Guarded automation failed for candidate {CandidateKey}.", candidate.CandidateKey);
                ModelState.AddModelError(string.Empty, $"Guarded automation skipped '{candidate.DisplayName}': {exception.Message}");
            }
        }

        if (acceptedCandidateKeys.Count == 0)
        {
            return;
        }

        CandidateDiscoveryResult = new SourceCandidateDiscoveryResponseDto
        {
            RequestedCategoryKeys = CandidateDiscoveryResult.RequestedCategoryKeys,
            Locale = CandidateDiscoveryResult.Locale,
            Market = CandidateDiscoveryResult.Market,
            AutomationMode = CandidateDiscoveryResult.AutomationMode,
            BrandHints = CandidateDiscoveryResult.BrandHints,
            GeneratedUtc = CandidateDiscoveryResult.GeneratedUtc,
            Diagnostics = CandidateDiscoveryResult.Diagnostics,
            Candidates = CandidateDiscoveryResult.Candidates.Select(candidate => acceptedCandidateKeys.Contains(candidate.CandidateKey)
                ? new SourceCandidateDto
                {
                    CandidateKey = candidate.CandidateKey,
                    DisplayName = candidate.DisplayName,
                    BaseUrl = candidate.BaseUrl,
                    Host = candidate.Host,
                    CandidateType = candidate.CandidateType,
                    AllowedMarkets = candidate.AllowedMarkets,
                    PreferredLocale = candidate.PreferredLocale,
                    MarketEvidence = candidate.MarketEvidence,
                    LocaleEvidence = candidate.LocaleEvidence,
                    ConfidenceScore = candidate.ConfidenceScore,
                    CrawlabilityScore = candidate.CrawlabilityScore,
                    ExtractabilityScore = candidate.ExtractabilityScore,
                    DuplicateRiskScore = candidate.DuplicateRiskScore,
                    RecommendationStatus = candidate.RecommendationStatus,
                    RuntimeExtractionStatus = candidate.RuntimeExtractionStatus,
                    RuntimeExtractionMessage = candidate.RuntimeExtractionMessage,
                    MatchedCategoryKeys = candidate.MatchedCategoryKeys,
                    MatchedBrandHints = candidate.MatchedBrandHints,
                    AlreadyRegistered = true,
                    DuplicateSourceIds = candidate.DuplicateSourceIds,
                    DuplicateSourceDisplayNames = candidate.DuplicateSourceDisplayNames,
                    AllowedByGovernance = candidate.AllowedByGovernance,
                    GovernanceWarning = candidate.GovernanceWarning,
                    Probe = candidate.Probe,
                    AutomationAssessment = candidate.AutomationAssessment,
                    Reasons = candidate.Reasons
                }
                : candidate).ToArray()
        };
            ApplyDismissedCandidates();

        await LoadAsync(cancellationToken);

        StatusMessage = seededJobIds.Count == 0
            ? $"Guarded automation auto-accepted {acceptedSources.Count} source(s): {string.Join(", ", acceptedSources)}."
            : $"Guarded automation auto-accepted {acceptedSources.Count} source(s) and seeded {seededJobIds.Count} crawl job(s): {string.Join(", ", seededJobIds)}.";
    }

    private RegisterSourceInput BuildRegistrationFromCandidate(SourceCandidateDto candidate, string automationMode)
    {
        var categoryKeys = NormalizeValues(candidate.MatchedCategoryKeys);
        if (categoryKeys.Count == 0)
        {
            categoryKeys = NormalizeValues(CandidateDiscovery.CategoryKeys);
        }

        return new RegisterSourceInput
        {
            SourceId = DeriveSourceId(new UseCandidateInput
            {
                CandidateKey = candidate.CandidateKey,
                DisplayName = candidate.DisplayName,
                BaseUrl = candidate.BaseUrl
            }),
            DisplayName = candidate.DisplayName.Trim(),
            BaseUrl = NormalizeBaseUrl(candidate.BaseUrl),
            Description = Registration.Description,
            IsEnabled = true,
            AllowedMarkets = ResolveCandidateAllowedMarkets(new UseCandidateInput
            {
                AllowedMarkets = candidate.AllowedMarkets.ToList()
            }),
            PreferredLocale = NormalizeOptionalText(candidate.PreferredLocale) ?? CandidateDiscovery.Locale ?? "en-GB",
            AutomationMode = NormalizeAutomationMode(automationMode),
            CategoryKeys = categoryKeys
        };
    }

    private RegisterSourceRequest BuildRegisterRequest(RegisterSourceInput registration)
    {
        return new RegisterSourceRequest
        {
            SourceId = registration.SourceId,
            DisplayName = registration.DisplayName,
            BaseUrl = registration.BaseUrl,
            Description = registration.Description,
            IsEnabled = registration.IsEnabled,
            AllowedMarkets = registration.AllowedMarkets,
            PreferredLocale = NormalizeOptionalText(registration.PreferredLocale),
            AutomationPolicy = new SourceAutomationPolicyDto
            {
                Mode = NormalizeAutomationMode(registration.AutomationMode)
            },
            SupportedCategoryKeys = registration.CategoryKeys
        };
    }

    private List<string> ResolveCandidateAllowedMarkets(UseCandidateInput candidate)
    {
        var candidateMarkets = NormalizeValues(candidate.AllowedMarkets);
        if (candidateMarkets.Count > 0)
        {
            return candidateMarkets;
        }

        var discoveryMarket = NormalizeOptionalText(CandidateDiscovery.Market);
        return string.IsNullOrWhiteSpace(discoveryMarket)
            ? ["UK"]
            : [discoveryMarket];
    }

    private static string DeriveSourceId(UseCandidateInput candidate)
    {
        var candidates = new[]
        {
            TryBuildSourceIdFromBaseUrl(candidate.BaseUrl),
            NormalizeSourceId(candidate.CandidateKey),
            NormalizeSourceId(candidate.DisplayName)
        };

        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string? TryBuildSourceIdFromBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.Trim();
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        return NormalizeSourceId(host);
    }

    private static string NormalizeSourceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Trim().Length);
        var lastWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (character is '.' or '-' or '_' or ' ')
            {
                if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string NormalizeBaseUrl(string value)
    {
        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        return value.Trim();
    }

    private static int NormalizeMaxCandidates(int value)
    {
        if (value <= 0)
        {
            return 10;
        }

        return Math.Min(25, value);
    }

    private static string NormalizeAutomationMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            SuggestAcceptMode => SuggestAcceptMode,
            AutoAcceptAndSeedMode => AutoAcceptAndSeedMode,
            _ => OperatorAssistedMode
        };
    }

    private void ApplyDismissedCandidates()
    {
        CandidateDiscovery.DismissedCandidateKeys = NormalizeValues(CandidateDiscovery.DismissedCandidateKeys);
    }

    private void AddDismissedCandidate(string? candidateKey)
    {
        CandidateDiscovery.DismissedCandidateKeys = NormalizeValues(
            CandidateDiscovery.DismissedCandidateKeys.Concat([candidateKey ?? string.Empty]));
    }

    private void RemoveDismissedCandidate(string? candidateKey)
    {
        CandidateDiscovery.DismissedCandidateKeys = NormalizeValues(
            CandidateDiscovery.DismissedCandidateKeys.Where(value => !string.Equals(value, candidateKey, StringComparison.OrdinalIgnoreCase)));
    }

    private bool IsDismissedCandidate(string? candidateKey)
    {
        return !string.IsNullOrWhiteSpace(candidateKey)
            && CandidateDiscovery.DismissedCandidateKeys.Contains(candidateKey, StringComparer.OrdinalIgnoreCase);
    }

    private static string FormatDuration(long durationMs)
    {
        return durationMs >= 1000
            ? $"{durationMs / 1000d:0.#}s"
            : $"{durationMs}ms";
    }

    public sealed class RegisterSourceInput
    {
        [Required]
        [Display(Name = "Source id")]
        public string SourceId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Url]
        [Display(Name = "Base URL")]
        public string BaseUrl { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        [Display(Name = "Allowed markets")]
        public List<string> AllowedMarkets { get; set; } = ["UK"];

        [Display(Name = "Preferred locale")]
        public string PreferredLocale { get; set; } = "en-GB";

        [Display(Name = "Automation mode")]
        public string AutomationMode { get; set; } = OperatorAssistedMode;

        public List<string> CategoryKeys { get; set; } = [];
    }

    public sealed class DiscoverSourceCandidatesInput
    {
        [Display(Name = "Categories")]
        public List<string> CategoryKeys { get; set; } = [];

        [Display(Name = "Locale")]
        public string? Locale { get; set; }

        [Display(Name = "Market")]
        public string? Market { get; set; }

        [Display(Name = "Brand hints")]
        public string? BrandHints { get; set; }

        [Display(Name = "Automation mode")]
        public string AutomationMode { get; set; } = OperatorAssistedMode;

        [Range(1, 25)]
        [Display(Name = "Max candidates")]
        public int MaxCandidates { get; set; } = 10;

        public List<string> DismissedCandidateKeys { get; set; } = [];
    }

    public sealed class UseCandidateInput
    {
        public string CandidateKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public List<string> AllowedMarkets { get; set; } = [];

        public string? PreferredLocale { get; set; }

        public List<string> CategoryKeys { get; set; } = [];
    }
}