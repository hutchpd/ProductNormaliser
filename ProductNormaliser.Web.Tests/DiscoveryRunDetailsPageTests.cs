using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class DiscoveryRunDetailsPageTests
{
    [Test]
    public async Task OnGetAsync_LoadsRunCandidatesAndAutoRefreshState()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(status: "running", stage: "llm_verify"),
            DiscoveryRunCandidates =
            [
                CreateCandidate("suggested_1", "suggested"),
                CreateCandidate("dismissed_1", "dismissed"),
                CreateCandidate("accepted_1", "manually_accepted")
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Run, Is.Not.Null);
            Assert.That(model.ShouldAutoRefresh, Is.True);
            Assert.That(model.ProgressPercent, Is.GreaterThan(0));
            Assert.That(model.ActiveCandidates.Select(candidate => candidate.CandidateKey), Is.EqualTo(new[] { "suggested_1", "accepted_1" }));
            Assert.That(model.ArchivedCandidates.Select(candidate => candidate.CandidateKey), Is.EqualTo(new[] { "dismissed_1" }));
            Assert.That(model.CanAcceptCandidate(model.ActiveCandidates[0]), Is.True);
            Assert.That(model.CanDismissCandidate(model.ArchivedCandidates[0]), Is.False);
            Assert.That(model.CanRestoreCandidate(model.ArchivedCandidates[0]), Is.True);
        });
    }

    [Test]
    public async Task OnPostPauseAsync_CallsApiAndRedirectsBackToRun()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(status: "running", stage: "probe")
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        var result = await model.OnPostPauseAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastPausedDiscoveryRunId, Is.EqualTo("discovery_run_1"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(((RedirectToPageResult)result).RouteValues!["runId"], Is.EqualTo("discovery_run_1"));
            Assert.That(model.StatusMessage, Does.Contain("Paused discovery run 'discovery_run_1'."));
        });
    }

    [Test]
    public async Task OnPostAcceptCandidateAsync_WhenApiFails_ReloadsPageWithError()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(status: "completed", stage: "publish"),
            DiscoveryRunCandidates = [CreateCandidate("safe_shop", "suggested")],
            DiscoveryRunException = new AdminApiException("Candidate is no longer available.")
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        var result = await model.OnPostAcceptCandidateAsync("safe_shop", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.ErrorMessage, Is.EqualTo("Candidate is no longer available."));
            Assert.That(model.Run, Is.Null);
            Assert.That(model.Candidates, Is.Empty);
        });
    }

    private static DiscoveryRunDto CreateRun(string status, string stage)
    {
        return new DiscoveryRunDto
        {
            RunId = "discovery_run_1",
            RequestedCategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = "suggest_accept",
            Status = status,
            CurrentStage = stage,
            StatusMessage = "Working.",
            LlmStatus = "active",
            LlmStatusMessage = "Loaded.",
            SearchResultCount = 4,
            CollapsedCandidateCount = 3,
            ProbeCompletedCount = 2,
            LlmQueueDepth = 1,
            LlmCompletedCount = 2,
            LlmTotalElapsedMs = 440,
            LlmAverageElapsedMs = 220,
            SuggestedCandidateCount = 1,
            AutoAcceptedCandidateCount = 1,
            PublishedCandidateCount = 1,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedUtc = DateTime.UtcNow.AddSeconds(-5)
        };
    }

    private static DiscoveryRunCandidateDto CreateCandidate(string candidateKey, string state)
    {
        return new DiscoveryRunCandidateDto
        {
            CandidateKey = candidateKey,
            State = state,
            DisplayName = candidateKey,
            BaseUrl = $"https://{candidateKey}.example/",
            Host = $"{candidateKey}.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MarketEvidence = "explicit",
            LocaleEvidence = "explicit",
            ConfidenceScore = 92m,
            CrawlabilityScore = 90m,
            ExtractabilityScore = 91m,
            DuplicateRiskScore = 2m,
            RecommendationStatus = "recommended",
            RuntimeExtractionStatus = "compatible",
            RuntimeExtractionMessage = "Compatible.",
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            Probe = new SourceCandidateProbeDto(),
            AutomationAssessment = new SourceCandidateAutomationAssessmentDto(),
            Reasons = []
        };
    }
}
