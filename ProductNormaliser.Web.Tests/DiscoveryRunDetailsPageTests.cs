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
                CreateCandidate("accepted_1", "manually_accepted"),
                CreateCandidate("duplicate_1", "superseded", supersededByCandidateKey: "accepted_1")
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
            Assert.That(model.ActivityLogEntries, Is.Not.Empty);
            Assert.That(model.ActiveCandidates.Select(candidate => candidate.CandidateKey), Is.EqualTo(new[] { "suggested_1", "accepted_1" }));
            Assert.That(model.ArchivedCandidates.Select(candidate => candidate.CandidateKey), Is.EqualTo(new[] { "dismissed_1", "duplicate_1" }));
            Assert.That(model.CanAcceptCandidate(model.ActiveCandidates[0]), Is.True);
            Assert.That(model.CanDismissCandidate(model.ArchivedCandidates[0]), Is.False);
            Assert.That(model.CanRestoreCandidate(model.ArchivedCandidates[0]), Is.True);
        });
    }

    [Test]
    public async Task OnGetAsync_BuildsActivityLogEntries_ForDiagnosticsAndArchivedCandidates()
    {
        var run = CreateRun(
            status: "completed",
            stage: "publish",
            searchElapsedMs: 2500,
            searchResultCount: 12,
            collapsedCandidateCount: 5,
            probeCompletedCount: 5,
            diagnostics:
            [
                new SourceCandidateDiscoveryDiagnosticDto
                {
                    Code = "search.provider.timeout",
                    Severity = "warning",
                    Title = "Provider timeout",
                    Message = "One upstream provider exceeded its response budget."
                }
            ]);

        var client = new FakeAdminApiClient
        {
            DiscoveryRun = run,
            DiscoveryRunCandidates =
            [
                CreateCandidate("dismissed_1", "dismissed", archivedUtc: DateTime.UtcNow.AddMinutes(-1), stateMessage: "Dismissed after manual review.", archiveReason: "Low confidence and duplicate risk."),
                CreateCandidate("suggested_1", "suggested")
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.ActivityLogEntries.Select(entry => entry.Title), Does.Contain("Discovery requested"));
            Assert.That(model.ActivityLogEntries.Select(entry => entry.Title), Does.Contain("Search completed"));
            Assert.That(model.ActivityLogEntries.Select(entry => entry.Title), Does.Contain("Provider timeout"));
            Assert.That(model.ActivityLogEntries.Select(entry => entry.Title), Does.Contain("Dismissed dismissed_1"));
            Assert.That(model.GetCandidateReasonSummary(model.ArchivedCandidates[0]), Does.Contain("Low confidence and duplicate risk."));
            Assert.That(model.GetArchiveActionLabel(model.ArchivedCandidates[0]), Is.EqualTo("Add back to review queue"));
        });
    }

    [Test]
    public async Task OnGetAsync_ComputesTimeoutAndFetchFailureBreakdown()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(
                status: "completed",
                stage: "publish",
                diagnostics:
                [
                    new SourceCandidateDiscoveryDiagnosticDto
                    {
                        Code = "search_timeout",
                        Severity = "warning",
                        Title = "Search provider timed out",
                        Message = "Search provider lookup exceeded the configured budget."
                    }
                ]),
            DiscoveryRunCandidates =
            [
                CreateCandidate(
                    "probe_timeout_candidate",
                    "failed",
                    probe: new SourceCandidateProbeDto
                    {
                        ProbeTimedOut = true,
                        ProbeElapsedMs = 15000
                    }),
                CreateCandidate(
                    "fetch_failed_candidate",
                    "suggested",
                    probe: new SourceCandidateProbeDto
                    {
                        RepresentativeCategoryPageFetchFailed = true,
                        RepresentativeProductPageFetchFailed = true
                    }),
                CreateCandidate(
                    "llm_timeout_candidate",
                    "suggested",
                    probe: new SourceCandidateProbeDto
                    {
                        LlmTimedOut = true,
                        LlmElapsedMs = 3500
                    })
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.SearchTimeoutCount, Is.EqualTo(1));
            Assert.That(model.ProbeTimeoutCandidateCount, Is.EqualTo(1));
            Assert.That(model.RepresentativePageFetchFailureCandidateCount, Is.EqualTo(1));
            Assert.That(model.RepresentativeCategoryFetchFailureCount, Is.EqualTo(1));
            Assert.That(model.RepresentativeProductFetchFailureCount, Is.EqualTo(1));
            Assert.That(model.LlmTimeoutCandidateCount, Is.EqualTo(1));
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

        var result = await model.OnPostAcceptCandidateAsync("safe_shop", 1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.ErrorMessage, Is.EqualTo("Candidate is no longer available."));
            Assert.That(model.Run, Is.Null);
            Assert.That(model.Candidates, Is.Empty);
        });
    }

    private static DiscoveryRunDto CreateRun(
        string status,
        string stage,
        long? searchElapsedMs = null,
        int searchResultCount = 4,
        int collapsedCandidateCount = 3,
        int probeCompletedCount = 2,
        IReadOnlyList<SourceCandidateDiscoveryDiagnosticDto>? diagnostics = null)
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
            SearchResultCount = searchResultCount,
            CollapsedCandidateCount = collapsedCandidateCount,
            ProbeCompletedCount = probeCompletedCount,
            LlmQueueDepth = 1,
            LlmCompletedCount = 2,
            LlmTotalElapsedMs = 440,
            LlmAverageElapsedMs = 220,
            SearchElapsedMs = searchElapsedMs,
            SuggestedCandidateCount = 1,
            AutoAcceptedCandidateCount = 1,
            PublishedCandidateCount = 1,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-5),
            StartedUtc = DateTime.UtcNow.AddMinutes(-4),
            UpdatedUtc = DateTime.UtcNow.AddSeconds(-5),
            Diagnostics = diagnostics ?? []
        };
    }

    [Test]
    public async Task OnPostAcceptCandidateAsync_PassesExpectedRevisionAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(status: "running", stage: "decide"),
            DiscoveryRunCandidates = [CreateCandidate("safe_shop", "suggested", revision: 7)]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        var result = await model.OnPostAcceptCandidateAsync("safe_shop", 7, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(client.LastAcceptedDiscoveryRunCandidateRunId, Is.EqualTo("discovery_run_1"));
            Assert.That(client.LastAcceptedDiscoveryRunCandidateKey, Is.EqualTo("safe_shop"));
            Assert.That(client.LastAcceptedDiscoveryRunCandidateRevision, Is.EqualTo(7));
            Assert.That(model.StatusMessage, Is.EqualTo("Accepted candidate 'safe_shop'."));
        });
    }

    [Test]
    public async Task OnPostDismissAndRestoreCandidateAsync_PassesExpectedRevision()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(status: "running", stage: "decide"),
            DiscoveryRunCandidates = [CreateCandidate("safe_shop", "suggested", revision: 3)]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        var dismissResult = await model.OnPostDismissCandidateAsync("safe_shop", 3, CancellationToken.None);
        var restoreResult = await model.OnPostRestoreCandidateAsync("safe_shop", 4, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(dismissResult, Is.TypeOf<RedirectToPageResult>());
            Assert.That(restoreResult, Is.TypeOf<RedirectToPageResult>());
            Assert.That(client.LastDismissedDiscoveryRunCandidateRevision, Is.EqualTo(3));
            Assert.That(client.LastRestoredDiscoveryRunCandidateRevision, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task OnPostAcceptCandidateAsync_WhenRevisionIsStale_ReloadsRunWithConflictError()
    {
        var client = new FakeAdminApiClient
        {
            DiscoveryRun = CreateRun(status: "running", stage: "decide"),
            DiscoveryRunCandidates = [CreateCandidate("safe_shop", "suggested", revision: 2)]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DiscoveryRuns.DetailsModel>.Instance)
        {
            RunId = "discovery_run_1"
        };

        var result = await model.OnPostAcceptCandidateAsync("safe_shop", 1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.ErrorMessage, Does.Contain("changed while this action was in progress"));
            Assert.That(model.Run, Is.Not.Null);
            Assert.That(model.Candidates, Has.Count.EqualTo(1));
        });
    }

    private static DiscoveryRunCandidateDto CreateCandidate(
        string candidateKey,
        string state,
        int revision = 1,
        string? supersededByCandidateKey = null,
        DateTime? archivedUtc = null,
        string? stateMessage = null,
        string? archiveReason = null,
        SourceCandidateProbeDto? probe = null)
    {
        return new DiscoveryRunCandidateDto
        {
            CandidateKey = candidateKey,
            Revision = revision,
            State = state,
            SupersededByCandidateKey = supersededByCandidateKey,
            ArchivedUtc = archivedUtc,
            StateMessage = stateMessage,
            ArchiveReason = archiveReason,
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
            Probe = probe ?? new SourceCandidateProbeDto(),
            AutomationAssessment = new SourceCandidateAutomationAssessmentDto(),
            Reasons = []
        };
    }
}
