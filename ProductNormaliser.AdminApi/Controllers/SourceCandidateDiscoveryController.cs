using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.Application.Sources;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/sources/candidates")]
public sealed class SourceCandidateDiscoveryController(ISourceCandidateDiscoveryService sourceCandidateDiscoveryService) : ControllerBase
{
    [HttpPost("discover")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(Contracts.SourceCandidateDiscoveryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Discover([FromBody] Contracts.DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await sourceCandidateDiscoveryService.DiscoverAsync(new DiscoverSourceCandidatesRequest
            {
                CategoryKeys = request.CategoryKeys,
                Locale = request.Locale,
                Market = request.Market,
                AutomationMode = request.AutomationMode,
                BrandHints = request.BrandHints,
                MaxCandidates = request.MaxCandidates
            }, cancellationToken);

            return Ok(new Contracts.SourceCandidateDiscoveryResponseDto
            {
                RequestedCategoryKeys = result.RequestedCategoryKeys,
                Locale = result.Locale,
                Market = result.Market,
                AutomationMode = result.AutomationMode,
                BrandHints = result.BrandHints,
                LlmStatus = result.LlmStatus,
                LlmStatusMessage = result.LlmStatusMessage,
                GeneratedUtc = result.GeneratedUtc,
                Diagnostics = result.Diagnostics.Select(diagnostic => new Contracts.SourceCandidateDiscoveryDiagnosticDto
                {
                    RecordedUtc = diagnostic.RecordedUtc,
                    Code = diagnostic.Code,
                    Severity = diagnostic.Severity,
                    Title = diagnostic.Title,
                    Message = diagnostic.Message
                }).ToArray(),
                Candidates = result.Candidates.Select(candidate => new Contracts.SourceCandidateDto
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
                    AlreadyRegistered = candidate.AlreadyRegistered,
                    DuplicateSourceIds = candidate.DuplicateSourceIds,
                    DuplicateSourceDisplayNames = candidate.DuplicateSourceDisplayNames,
                    AllowedByGovernance = candidate.AllowedByGovernance,
                    GovernanceWarning = candidate.GovernanceWarning,
                    Probe = new Contracts.SourceCandidateProbeDto
                    {
                        HomePageReachable = candidate.Probe.HomePageReachable,
                        RobotsTxtReachable = candidate.Probe.RobotsTxtReachable,
                        SitemapDetected = candidate.Probe.SitemapDetected,
                        SitemapUrls = candidate.Probe.SitemapUrls,
                        CrawlabilityScore = candidate.Probe.CrawlabilityScore,
                        CategoryRelevanceScore = candidate.Probe.CategoryRelevanceScore,
                        ExtractabilityScore = candidate.Probe.ExtractabilityScore,
                        CatalogLikelihoodScore = candidate.Probe.CatalogLikelihoodScore,
                        RepresentativeCategoryPageUrl = candidate.Probe.RepresentativeCategoryPageUrl,
                        RepresentativeCategoryPageReachable = candidate.Probe.RepresentativeCategoryPageReachable,
                        RepresentativeProductPageUrl = candidate.Probe.RepresentativeProductPageUrl,
                        RepresentativeProductPageReachable = candidate.Probe.RepresentativeProductPageReachable,
                        RuntimeExtractionCompatible = candidate.Probe.RuntimeExtractionCompatible,
                        RepresentativeRuntimeProductCount = candidate.Probe.RepresentativeRuntimeProductCount,
                        ProbeAttemptCount = candidate.Probe.ProbeAttemptCount,
                        ProbeElapsedMs = candidate.Probe.ProbeElapsedMs,
                        LlmElapsedMs = candidate.Probe.LlmElapsedMs,
                        LlmBudgetMs = candidate.Probe.LlmBudgetMs,
                        LlmBudgetLimitedByProbe = candidate.Probe.LlmBudgetLimitedByProbe,
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
                }).ToArray()
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationErrors(exception)));
        }
    }

    private static Dictionary<string, string[]> CreateValidationErrors(ArgumentException exception)
    {
        return new Dictionary<string, string[]>
        {
            [string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName] = [exception.Message]
        };
    }
}