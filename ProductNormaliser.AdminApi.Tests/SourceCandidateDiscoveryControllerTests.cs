using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.Application.Sources;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class SourceCandidateDiscoveryControllerTests
{
    [Test]
    public async Task Discover_ReturnsMappedCandidateDtos()
    {
        var controller = new SourceCandidateDiscoveryController(new FakeSourceCandidateDiscoveryService(new SourceCandidateDiscoveryResult
        {
            RequestedCategoryKeys = ["tv"],
            Locale = "en-GB",
            Market = "UK",
            BrandHints = ["Samsung"],
            GeneratedUtc = new DateTime(2026, 03, 25, 12, 00, 00, DateTimeKind.Utc),
            Candidates =
            [
                new SourceCandidateResult
                {
                    CandidateKey = "samsung_example",
                    DisplayName = "Samsung Official Store",
                    BaseUrl = "https://samsung.example/",
                    Host = "samsung.example",
                    CandidateType = "manufacturer",
                    ConfidenceScore = 82m,
                    CrawlabilityScore = 90m,
                    ExtractabilityScore = 88m,
                    DuplicateRiskScore = 0m,
                    RecommendationStatus = SourceCandidateResult.RecommendationRecommended,
                    RuntimeExtractionStatus = SourceCandidateResult.RuntimeExtractionCompatibleStatus,
                    RuntimeExtractionMessage = "Representative runtime extraction succeeded on the sampled product page.",
                    MatchedCategoryKeys = ["tv"],
                    MatchedBrandHints = ["Samsung"],
                    AlreadyRegistered = false,
                    AllowedByGovernance = true,
                    Probe = new SourceCandidateProbeResult
                    {
                        HomePageReachable = true,
                        RobotsTxtReachable = true,
                        SitemapDetected = true,
                        SitemapUrls = ["https://samsung.example/sitemap.xml"],
                        CrawlabilityScore = 90m,
                        CategoryRelevanceScore = 18m,
                        ExtractabilityScore = 88m,
                        CatalogLikelihoodScore = 72m,
                        RepresentativeCategoryPageUrl = "https://samsung.example/tv/",
                        RepresentativeCategoryPageReachable = true,
                        RepresentativeProductPageUrl = "https://samsung.example/product/oled-1",
                        RepresentativeProductPageReachable = true,
                        RuntimeExtractionCompatible = true,
                        RepresentativeRuntimeProductCount = 1,
                        StructuredProductEvidenceDetected = true,
                        TechnicalAttributeEvidenceDetected = true,
                        CategoryPageHints = ["https://samsung.example/tv/"],
                        LikelyListingUrlPatterns = ["/tv/"],
                        LikelyProductUrlPatterns = ["/product/"]
                    },
                    Reasons =
                    [
                        new SourceCandidateReason
                        {
                            Code = "search_match",
                            Message = "Matched official store results.",
                            Weight = 10m
                        }
                    ]
                }
            ]
        }));

        var result = await controller.Discover(new ProductNormaliser.AdminApi.Contracts.DiscoverSourceCandidatesRequest
        {
            CategoryKeys = ["tv"],
            Locale = "en-GB",
            Market = "UK",
            BrandHints = ["Samsung"],
            MaxCandidates = 5
        });

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as ProductNormaliser.AdminApi.Contracts.SourceCandidateDiscoveryResponseDto;
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.RequestedCategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(payload.Locale, Is.EqualTo("en-GB"));
            Assert.That(payload.Candidates, Has.Count.EqualTo(1));
            Assert.That(payload.Candidates[0].DisplayName, Is.EqualTo("Samsung Official Store"));
            Assert.That(payload.Candidates[0].RecommendationStatus, Is.EqualTo("recommended"));
            Assert.That(payload.Candidates[0].RuntimeExtractionStatus, Is.EqualTo("compatible"));
            Assert.That(payload.Candidates[0].RuntimeExtractionMessage, Is.EqualTo("Representative runtime extraction succeeded on the sampled product page."));
            Assert.That(payload.Candidates[0].Probe.SitemapUrls, Is.EqualTo(new[] { "https://samsung.example/sitemap.xml" }));
            Assert.That(payload.Candidates[0].Probe.RuntimeExtractionCompatible, Is.True);
            Assert.That(payload.Candidates[0].Probe.RepresentativeRuntimeProductCount, Is.EqualTo(1));
            Assert.That(payload.Candidates[0].Probe.StructuredProductEvidenceDetected, Is.True);
            Assert.That(payload.Candidates[0].Reasons[0].Code, Is.EqualTo("search_match"));
        });
    }

    [Test]
    public async Task Discover_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var controller = new SourceCandidateDiscoveryController(new FakeSourceCandidateDiscoveryService(new ArgumentException("Unknown category keys: smartwatch.", "request")));

        var result = await controller.Discover(new ProductNormaliser.AdminApi.Contracts.DiscoverSourceCandidatesRequest
        {
            CategoryKeys = ["smartwatch"]
        });

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        var payload = badRequest.Value as ValidationProblemDetails;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Errors["request"], Has.Length.EqualTo(1));
        Assert.That(payload.Errors["request"][0], Does.StartWith("Unknown category keys: smartwatch."));
    }

    private sealed class FakeSourceCandidateDiscoveryService : ISourceCandidateDiscoveryService
    {
        private readonly SourceCandidateDiscoveryResult? result;
        private readonly Exception? exception;

        public FakeSourceCandidateDiscoveryService(SourceCandidateDiscoveryResult result)
        {
            this.result = result;
        }

        public FakeSourceCandidateDiscoveryService(Exception exception)
        {
            this.exception = exception;
        }

        public Task<SourceCandidateDiscoveryResult> DiscoverAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
        {
            _ = request;
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(result!);
        }
    }
}