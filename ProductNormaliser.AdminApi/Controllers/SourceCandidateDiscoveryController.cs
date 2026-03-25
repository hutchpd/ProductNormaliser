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
                BrandHints = request.BrandHints,
                MaxCandidates = request.MaxCandidates
            }, cancellationToken);

            return Ok(new Contracts.SourceCandidateDiscoveryResponseDto
            {
                RequestedCategoryKeys = result.RequestedCategoryKeys,
                Locale = result.Locale,
                Market = result.Market,
                BrandHints = result.BrandHints,
                GeneratedUtc = result.GeneratedUtc,
                Candidates = result.Candidates.Select(candidate => new Contracts.SourceCandidateDto
                {
                    CandidateKey = candidate.CandidateKey,
                    DisplayName = candidate.DisplayName,
                    BaseUrl = candidate.BaseUrl,
                    Host = candidate.Host,
                    CandidateType = candidate.CandidateType,
                    AllowedMarkets = candidate.AllowedMarkets,
                    PreferredLocale = candidate.PreferredLocale,
                    ConfidenceScore = candidate.ConfidenceScore,
                    CrawlabilityScore = candidate.CrawlabilityScore,
                    ExtractabilityScore = candidate.ExtractabilityScore,
                    DuplicateRiskScore = candidate.DuplicateRiskScore,
                    RecommendationStatus = candidate.RecommendationStatus,
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
                        StructuredProductEvidenceDetected = candidate.Probe.StructuredProductEvidenceDetected,
                        TechnicalAttributeEvidenceDetected = candidate.Probe.TechnicalAttributeEvidenceDetected,
                        NonCatalogContentHeavy = candidate.Probe.NonCatalogContentHeavy,
                        CategoryPageHints = candidate.Probe.CategoryPageHints,
                        LikelyListingUrlPatterns = candidate.Probe.LikelyListingUrlPatterns,
                        LikelyProductUrlPatterns = candidate.Probe.LikelyProductUrlPatterns
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