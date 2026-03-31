using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/sources/discovery-runs")]
public sealed class DiscoveryRunsController(IDiscoveryRunService discoveryRunService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var results = await discoveryRunService.ListAsync(new DiscoveryRunQuery
        {
            Status = status,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 50)
        }, cancellationToken);

        return Ok(new Contracts.DiscoveryRunPageDto
        {
            Items = results.Items.Select(Map).ToArray(),
            Page = results.Page,
            PageSize = results.PageSize,
            TotalCount = results.TotalCount,
            TotalPages = results.TotalPages
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] Contracts.CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await discoveryRunService.CreateAsync(new CreateDiscoveryRunRequest
            {
                CategoryKeys = request.CategoryKeys,
                Locale = request.Locale,
                Market = request.Market,
                AutomationMode = request.AutomationMode,
                BrandHints = request.BrandHints,
                MaxCandidates = request.MaxCandidates
            }, cancellationToken);

            return CreatedAtAction(nameof(Get), new { runId = run.RunId }, Map(run));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(exception));
        }
    }

    [HttpGet("{runId}")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string runId, CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunService.GetAsync(runId, cancellationToken);
        return run is null ? NotFound() : Ok(Map(run));
    }

    [HttpGet("{runId}/candidates")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunCandidatePageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCandidates(string runId, [FromQuery] Contracts.DiscoveryRunCandidateQueryDto? query, CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunService.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var candidates = await discoveryRunService.QueryCandidatesAsync(runId, new DiscoveryRunCandidateQuery
        {
            StateFilter = query?.StateFilter,
            Sort = query?.Sort,
            Page = query?.Page ?? 1,
            PageSize = query?.PageSize ?? 12
        }, cancellationToken);

        return Ok(new Contracts.DiscoveryRunCandidatePageDto
        {
            Items = candidates.Items.Select(Map).ToArray(),
            StateFilter = candidates.StateFilter,
            Sort = candidates.Sort,
            Page = candidates.Page,
            PageSize = candidates.PageSize,
            TotalCount = candidates.TotalCount,
            TotalPages = candidates.TotalPages,
            Summary = new Contracts.DiscoveryRunCandidateRunSummaryDto
            {
                RunCandidateCount = candidates.Summary.RunCandidateCount,
                ActiveCandidateCount = candidates.Summary.ActiveCandidateCount,
                ArchivedCandidateCount = candidates.Summary.ArchivedCandidateCount,
                LlmMeasuredCandidateCount = candidates.Summary.LlmMeasuredCandidateCount,
                LlmBudgetProbeCappedCandidateCount = candidates.Summary.LlmBudgetProbeCappedCandidateCount,
                ProbeTimeoutCandidateCount = candidates.Summary.ProbeTimeoutCandidateCount,
                RepresentativePageFetchFailureCandidateCount = candidates.Summary.RepresentativePageFetchFailureCandidateCount,
                RepresentativeCategoryFetchFailureCount = candidates.Summary.RepresentativeCategoryFetchFailureCount,
                RepresentativeProductFetchFailureCount = candidates.Summary.RepresentativeProductFetchFailureCount,
                LlmTimeoutCandidateCount = candidates.Summary.LlmTimeoutCandidateCount,
                AverageLlmBudgetMs = candidates.Summary.AverageLlmBudgetMs,
                AverageLlmBudgetUtilizationPercent = candidates.Summary.AverageLlmBudgetUtilizationPercent,
                AutoAcceptBlockers = candidates.Summary.AutoAcceptBlockers.Select(blocker => new Contracts.DiscoveryRunCandidateBlockerSummaryDto
                {
                    Code = blocker.Code,
                    Label = blocker.Label,
                    Count = blocker.Count
                }).ToArray()
            }
        });
    }

    [HttpPost("{runId}/pause")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Pause(string runId, CancellationToken cancellationToken = default)
        => MutateRunAsync(() => discoveryRunService.PauseAsync(runId, cancellationToken));

    [HttpPost("{runId}/resume")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Resume(string runId, CancellationToken cancellationToken = default)
        => MutateRunAsync(() => discoveryRunService.ResumeAsync(runId, cancellationToken));

    [HttpPost("{runId}/stop")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Stop(string runId, CancellationToken cancellationToken = default)
        => MutateRunAsync(() => discoveryRunService.StopAsync(runId, cancellationToken));

    [HttpPost("{runId}/candidates/{candidateKey}/accept")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> AcceptCandidate(string runId, string candidateKey, [FromBody] Contracts.DiscoveryRunCandidateMutationRequest request, CancellationToken cancellationToken = default)
        => MutateCandidateAsync(() => discoveryRunService.AcceptCandidateAsync(runId, candidateKey, request.ExpectedRevision, cancellationToken));

    [HttpPost("{runId}/candidates/{candidateKey}/dismiss")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> DismissCandidate(string runId, string candidateKey, [FromBody] Contracts.DiscoveryRunCandidateMutationRequest request, CancellationToken cancellationToken = default)
        => MutateCandidateAsync(() => discoveryRunService.DismissCandidateAsync(runId, candidateKey, request.ExpectedRevision, cancellationToken));

    [HttpPost("{runId}/candidates/{candidateKey}/restore")]
    [ProducesResponseType(typeof(Contracts.DiscoveryRunCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> RestoreCandidate(string runId, string candidateKey, [FromBody] Contracts.DiscoveryRunCandidateMutationRequest request, CancellationToken cancellationToken = default)
        => MutateCandidateAsync(() => discoveryRunService.RestoreCandidateAsync(runId, candidateKey, request.ExpectedRevision, cancellationToken));

    private async Task<IActionResult> MutateRunAsync(Func<Task<DiscoveryRun?>> action)
    {
        try
        {
            var run = await action();
            return run is null ? NotFound() : Ok(Map(run));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblem(exception));
        }
    }

    private async Task<IActionResult> MutateCandidateAsync(Func<Task<DiscoveryRunCandidate?>> action)
    {
        try
        {
            var candidate = await action();
            return candidate is null ? NotFound() : Ok(Map(candidate));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblem(exception));
        }
    }

    private static ValidationProblemDetails CreateProblem(Exception exception)
    {
        return new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["request"] = [exception.Message]
        });
    }

    private static Contracts.DiscoveryRunDto Map(DiscoveryRun run)
    {
        return new Contracts.DiscoveryRunDto
        {
            RunId = run.RunId,
            TriggerKind = run.TriggerKind,
            RecurringCampaignId = run.RecurringCampaignId,
            RequestedCategoryKeys = run.RequestedCategoryKeys,
            Locale = run.Locale,
            Market = run.Market,
            AutomationMode = run.AutomationMode,
            BrandHints = run.BrandHints,
            MaxCandidates = run.MaxCandidates,
            Status = run.Status,
            CurrentStage = run.CurrentStage,
            StatusMessage = run.StatusMessage,
            FailureMessage = run.FailureMessage,
            LlmStatus = run.LlmStatus,
            LlmStatusMessage = run.LlmStatusMessage,
            SearchResultCount = run.SearchResultCount,
            CollapsedCandidateCount = run.CollapsedCandidateCount,
            ProbeCompletedCount = run.ProbeCompletedCount,
            ProbeTotalElapsedMs = run.ProbeTotalElapsedMs,
            ProbeAverageElapsedMs = run.ProbeAverageElapsedMs,
            LlmQueueDepth = run.LlmQueueDepth,
            LlmCompletedCount = run.LlmCompletedCount,
            LlmTotalElapsedMs = run.LlmTotalElapsedMs,
            LlmAverageElapsedMs = run.LlmAverageElapsedMs,
            SearchElapsedMs = run.SearchElapsedMs,
            SearchTimeoutBudgetMs = run.SearchTimeoutBudgetMs,
            ProbeTimeoutBudgetMs = run.ProbeTimeoutBudgetMs,
            LlmTimeoutBudgetMs = run.LlmTimeoutBudgetMs,
            SuggestedCandidateCount = run.SuggestedCandidateCount,
            AutoAcceptedCandidateCount = run.AutoAcceptedCandidateCount,
            PublishedCandidateCount = run.PublishedCandidateCount,
            CandidateThroughputPerMinute = run.CandidateThroughputPerMinute,
            AcceptanceRate = run.AcceptanceRate,
            ManualReviewRate = run.ManualReviewRate,
            TimeToFirstAcceptedCandidateMs = run.TimeToFirstAcceptedCandidateMs,
            FirstAcceptedUtc = run.FirstAcceptedUtc,
            RecoveryAttemptCount = run.RecoveryAttemptCount,
            CreatedUtc = run.CreatedUtc,
            UpdatedUtc = run.UpdatedUtc,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            CancelRequestedUtc = run.CancelRequestedUtc,
            Diagnostics = run.Diagnostics.Select(diagnostic => new Contracts.SourceCandidateDiscoveryDiagnosticDto
            {
                Code = diagnostic.Code,
                Severity = diagnostic.Severity,
                Title = diagnostic.Title,
                Message = diagnostic.Message
            }).ToArray()
        };
    }

    private static Contracts.DiscoveryRunCandidateDto Map(DiscoveryRunCandidate candidate)
    {
        return new Contracts.DiscoveryRunCandidateDto
        {
            CandidateKey = candidate.CandidateKey,
            Revision = candidate.Revision,
            State = candidate.State,
            PreviousState = candidate.PreviousState,
            SupersededByCandidateKey = candidate.SupersededByCandidateKey,
            AcceptedSourceId = candidate.AcceptedSourceId,
            StateMessage = candidate.StateMessage,
            ArchiveReason = candidate.ArchiveReason,
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
            AlreadyRegistered = candidate.AlreadyRegistered,
            DuplicateSourceIds = candidate.DuplicateSourceIds,
            DuplicateSourceDisplayNames = candidate.DuplicateSourceDisplayNames,
            AllowedByGovernance = candidate.AllowedByGovernance,
            GovernanceWarning = candidate.GovernanceWarning,
            ArchivedUtc = candidate.ArchivedUtc,
            Probe = new Contracts.SourceCandidateProbeDto
            {
                HomePageReachable = candidate.Probe.HomePageReachable,
                RobotsTxtReachable = candidate.Probe.RobotsTxtReachable,
                ProbeTimedOut = candidate.Probe.ProbeTimedOut,
                ProbeFailed = candidate.Probe.ProbeFailed,
                SitemapDetected = candidate.Probe.SitemapDetected,
                SitemapUrls = candidate.Probe.SitemapUrls,
                CrawlabilityScore = candidate.Probe.CrawlabilityScore,
                CategoryRelevanceScore = candidate.Probe.CategoryRelevanceScore,
                ExtractabilityScore = candidate.Probe.ExtractabilityScore,
                CatalogLikelihoodScore = candidate.Probe.CatalogLikelihoodScore,
                RepresentativeCategoryPageUrl = candidate.Probe.RepresentativeCategoryPageUrl,
                RepresentativeCategoryPageReachable = candidate.Probe.RepresentativeCategoryPageReachable,
                RepresentativeCategoryPageFetchFailed = candidate.Probe.RepresentativeCategoryPageFetchFailed,
                RepresentativeProductPageUrl = candidate.Probe.RepresentativeProductPageUrl,
                RepresentativeProductPageReachable = candidate.Probe.RepresentativeProductPageReachable,
                RepresentativeProductPageFetchFailed = candidate.Probe.RepresentativeProductPageFetchFailed,
                RuntimeExtractionCompatible = candidate.Probe.RuntimeExtractionCompatible,
                RepresentativeRuntimeProductCount = candidate.Probe.RepresentativeRuntimeProductCount,
                ProbeAttemptCount = candidate.Probe.ProbeAttemptCount,
                ProbeElapsedMs = candidate.Probe.ProbeElapsedMs,
                LlmElapsedMs = candidate.Probe.LlmElapsedMs,
                LlmBudgetMs = candidate.Probe.LlmBudgetMs,
                LlmBudgetLimitedByProbe = candidate.Probe.LlmBudgetLimitedByProbe,
                LlmTimedOut = candidate.Probe.LlmTimedOut,
                StructuredProductEvidenceDetected = candidate.Probe.StructuredProductEvidenceDetected,
                TechnicalAttributeEvidenceDetected = candidate.Probe.TechnicalAttributeEvidenceDetected,
                NonCatalogContentHeavy = candidate.Probe.NonCatalogContentHeavy,
                CategoryPageHints = candidate.Probe.CategoryPageHints,
                LikelyListingUrlPatterns = candidate.Probe.LikelyListingUrlPatterns,
                LikelyProductUrlPatterns = candidate.Probe.LikelyProductUrlPatterns
            },
            AutomationAssessment = new Contracts.SourceCandidateAutomationAssessmentDto
            {
                RequestedMode = candidate.AutomationAssessment.RequestedMode,
                Decision = candidate.AutomationAssessment.Decision,
                MarketMatchApproved = candidate.AutomationAssessment.MarketMatchApproved,
                MarketEvidenceStrongEnough = candidate.AutomationAssessment.MarketEvidenceStrongEnough,
                GovernancePassed = candidate.AutomationAssessment.GovernancePassed,
                DuplicateRiskAccepted = candidate.AutomationAssessment.DuplicateRiskAccepted,
                RepresentativeValidationPassed = candidate.AutomationAssessment.RepresentativeValidationPassed,
                ExtractabilityConfidencePassed = candidate.AutomationAssessment.ExtractabilityConfidencePassed,
                YieldConfidencePassed = candidate.AutomationAssessment.YieldConfidencePassed,
                SuggestionBreadthPassed = candidate.AutomationAssessment.SuggestionBreadthPassed,
                AutoAcceptBreadthPassed = candidate.AutomationAssessment.AutoAcceptBreadthPassed,
                LocaleAligned = candidate.AutomationAssessment.LocaleAligned,
                CrawlabilityPassed = candidate.AutomationAssessment.CrawlabilityPassed,
                CategoryRelevancePassed = candidate.AutomationAssessment.CategoryRelevancePassed,
                CatalogLikelihoodPassed = candidate.AutomationAssessment.CatalogLikelihoodPassed,
                SuggestionConfidencePassed = candidate.AutomationAssessment.SuggestionConfidencePassed,
                AutoAcceptConfidencePassed = candidate.AutomationAssessment.AutoAcceptConfidencePassed,
                EligibleForSuggestion = candidate.AutomationAssessment.EligibleForSuggestion,
                EligibleForAutoAccept = candidate.AutomationAssessment.EligibleForAutoAccept,
                EligibleForAutoSeed = candidate.AutomationAssessment.EligibleForAutoSeed,
                MarketEvidence = candidate.AutomationAssessment.MarketEvidence,
                LocaleEvidence = candidate.AutomationAssessment.LocaleEvidence,
                SupportingReasons = candidate.AutomationAssessment.SupportingReasons,
                BlockingReasons = candidate.AutomationAssessment.BlockingReasons
            },
            Reasons = candidate.Reasons.Select(reason => new Contracts.SourceCandidateReasonDto
            {
                Code = reason.Code,
                Message = reason.Message,
                Weight = reason.Weight
            }).ToArray()
        };
    }
}