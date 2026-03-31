using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class DiscoveryRunsControllerTests
{
    [Test]
    public async Task List_ReturnsPagedRunContractsAndPassesQueryToService()
    {
        var run = CreateRun("discovery_run_1", DiscoveryRunStatuses.Recoverable);
        run.SearchElapsedMs = 321;
        run.SearchTimeoutBudgetMs = 20000;
        run.ProbeTimeoutBudgetMs = 12000;
        run.LlmTimeoutBudgetMs = 15000;
        run.ProbeTotalElapsedMs = 480;
        run.ProbeAverageElapsedMs = 240;
        run.LlmQueueDepth = 2;
        run.LlmCompletedCount = 1;
        run.LlmTotalElapsedMs = 125;
        run.LlmAverageElapsedMs = 125;
        run.CandidateThroughputPerMinute = 3.5m;
        run.AcceptanceRate = 0.5m;
        run.ManualReviewRate = 0.5m;
        run.TimeToFirstAcceptedCandidateMs = 900;
        run.FirstAcceptedUtc = DateTime.UtcNow.AddMinutes(-1);
        run.RecoveryAttemptCount = 1;

        var service = new FakeDiscoveryRunService
        {
            CreatedRun = run
        };
        var controller = new DiscoveryRunsController(service);

        var result = await controller.List(DiscoveryRunStatuses.Recoverable, 2, 15, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result;
        var dto = ok.Value as DiscoveryRunPageDto;

        Assert.Multiple(() =>
        {
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.Items, Has.Count.EqualTo(1));
            Assert.That(dto.Page, Is.EqualTo(2));
            Assert.That(dto.PageSize, Is.EqualTo(15));
            Assert.That(dto.TotalCount, Is.EqualTo(1));
            Assert.That(dto.TotalPages, Is.EqualTo(1));
            Assert.That(dto.Items[0].RunId, Is.EqualTo("discovery_run_1"));
            Assert.That(dto.Items[0].Status, Is.EqualTo(DiscoveryRunStatuses.Recoverable));
            Assert.That(dto.Items[0].SearchElapsedMs, Is.EqualTo(321));
            Assert.That(dto.Items[0].ProbeAverageElapsedMs, Is.EqualTo(240));
            Assert.That(dto.Items[0].LlmTimeoutBudgetMs, Is.EqualTo(15000));
            Assert.That(dto.Items[0].RecoveryAttemptCount, Is.EqualTo(1));
            Assert.That(service.LastListQuery, Is.Not.Null);
            Assert.That(service.LastListQuery!.Status, Is.EqualTo(DiscoveryRunStatuses.Recoverable));
            Assert.That(service.LastListQuery.Page, Is.EqualTo(2));
            Assert.That(service.LastListQuery.PageSize, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task Create_ReturnsCreatedContract()
    {
        var service = new FakeDiscoveryRunService
        {
            CreatedRun = CreateRun("discovery_run_1", DiscoveryRunStatuses.Queued)
        };
        var controller = new DiscoveryRunsController(service);

        var result = await controller.Create(new ProductNormaliser.AdminApi.Contracts.CreateDiscoveryRunRequest
        {
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.SuggestAccept,
            BrandHints = ["Sony"],
            MaxCandidates = 12
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var created = (CreatedAtActionResult)result;
        var dto = created.Value as DiscoveryRunDto;

        Assert.Multiple(() =>
        {
            Assert.That(created.ActionName, Is.EqualTo("Get"));
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.RunId, Is.EqualTo("discovery_run_1"));
            Assert.That(dto.RequestedCategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(dto.Status, Is.EqualTo(DiscoveryRunStatuses.Queued));
        });
    }

    [Test]
    public async Task GetCandidates_ReturnsNotFoundWhenRunDoesNotExist()
    {
        var controller = new DiscoveryRunsController(new FakeDiscoveryRunService());

        var result = await controller.GetCandidates("missing", null, CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetCandidates_ReturnsPagedContractsAndPassesQueryToService()
    {
        var service = new FakeDiscoveryRunService
        {
            CreatedRun = CreateRun("discovery_run_1", DiscoveryRunStatuses.Running),
            RestoredCandidate = CreateCandidate("safe_shop", DiscoveryRunCandidateStates.Suggested)
        };
        var controller = new DiscoveryRunsController(service);

        var result = await controller.GetCandidates(
            "discovery_run_1",
            new DiscoveryRunCandidateQueryDto
            {
                StateFilter = DiscoveryRunCandidateStateFilters.Active,
                Sort = DiscoveryRunCandidateSortModes.ReviewPriority,
                Page = 2,
                PageSize = 25
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result;
        var dto = ok.Value as DiscoveryRunCandidatePageDto;

        Assert.Multiple(() =>
        {
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.Items, Has.Count.EqualTo(1));
            Assert.That(dto.Page, Is.EqualTo(2));
            Assert.That(dto.PageSize, Is.EqualTo(25));
            Assert.That(dto.TotalCount, Is.EqualTo(1));
            Assert.That(dto.Items[0].CandidateKey, Is.EqualTo("safe_shop"));
            Assert.That(dto.Summary.ActiveCandidateCount, Is.EqualTo(1));
            Assert.That(dto.Summary.LlmMeasuredCandidateCount, Is.EqualTo(1));
            Assert.That(dto.Summary.LlmBudgetProbeCappedCandidateCount, Is.EqualTo(1));
            Assert.That(dto.Summary.AverageLlmBudgetMs, Is.EqualTo(200));
            Assert.That(dto.Summary.AverageLlmBudgetUtilizationPercent, Is.EqualTo(50m));
            Assert.That(dto.Summary.AutoAcceptBlockers.Any(blocker => blocker.Code == "duplicate_risk_high" && blocker.Count == 1), Is.True);
            Assert.That(service.LastCandidateQuery, Is.Not.Null);
            Assert.That(service.LastCandidateQuery!.StateFilter, Is.EqualTo(DiscoveryRunCandidateStateFilters.Active));
            Assert.That(service.LastCandidateQuery.Sort, Is.EqualTo(DiscoveryRunCandidateSortModes.ReviewPriority));
            Assert.That(service.LastCandidateQuery.Page, Is.EqualTo(2));
            Assert.That(service.LastCandidateQuery.PageSize, Is.EqualTo(25));
        });
    }

    [Test]
    public async Task Pause_ReturnsConflictProblemForInvalidTransition()
    {
        var service = new FakeDiscoveryRunService
        {
            PauseException = new InvalidOperationException("Discovery run 'discovery_run_1' cannot be paused from status 'completed'.")
        };
        var controller = new DiscoveryRunsController(service);

        var result = await controller.Pause("discovery_run_1", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        var conflict = (ConflictObjectResult)result;
        var problem = conflict.Value as ValidationProblemDetails;

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors["request"], Does.Contain("Discovery run 'discovery_run_1' cannot be paused from status 'completed'."));
        });
    }

    [Test]
    public async Task RestoreCandidate_ReturnsMappedCandidateContract()
    {
        var service = new FakeDiscoveryRunService
        {
            RestoredCandidate = CreateCandidate("safe_shop", DiscoveryRunCandidateStates.Suggested)
        };
        var controller = new DiscoveryRunsController(service);

        var result = await controller.RestoreCandidate("discovery_run_1", "safe_shop", new DiscoveryRunCandidateMutationRequest { ExpectedRevision = 4 }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result;
        var dto = ok.Value as DiscoveryRunCandidateDto;

        Assert.Multiple(() =>
        {
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.CandidateKey, Is.EqualTo("safe_shop"));
            Assert.That(dto.Revision, Is.EqualTo(4));
            Assert.That(dto.State, Is.EqualTo(DiscoveryRunCandidateStates.Suggested));
            Assert.That(dto.DisplayName, Is.EqualTo("Safe Shop"));
            Assert.That(service.LastRestoreExpectedRevision, Is.EqualTo(4));
        });
    }

    private static DiscoveryRun CreateRun(string runId, string status)
    {
        return new DiscoveryRun
        {
            RunId = runId,
            RequestedCategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.SuggestAccept,
            Status = status,
            CurrentStage = DiscoveryRunStageNames.Search,
            StatusMessage = "Queued.",
            LlmStatus = "disabled",
            LlmStatusMessage = "Disabled.",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private static DiscoveryRunCandidate CreateCandidate(string candidateKey, string state)
    {
        return new DiscoveryRunCandidate
        {
            Id = $"discovery_run_1:{candidateKey}",
            RunId = "discovery_run_1",
            CandidateKey = candidateKey,
            Revision = 4,
            State = state,
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe.example/",
            Host = "safe.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MarketEvidence = "explicit",
            LocaleEvidence = "explicit",
            ConfidenceScore = 91m,
            CrawlabilityScore = 90m,
            ExtractabilityScore = 89m,
            DuplicateRiskScore = 1m,
            RecommendationStatus = SourceCandidateResult.RecommendationRecommended,
            RuntimeExtractionStatus = SourceCandidateResult.RuntimeExtractionCompatibleStatus,
            RuntimeExtractionMessage = "Compatible.",
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            Probe = new DiscoveryRunCandidateProbe
            {
                LlmElapsedMs = 100,
                LlmBudgetMs = 200,
                LlmBudgetLimitedByProbe = true
            },
            AutomationAssessment = new DiscoveryRunCandidateAutomationAssessment(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private sealed class FakeDiscoveryRunService : IDiscoveryRunService
    {
        public DiscoveryRun? CreatedRun { get; set; }
        public InvalidOperationException? PauseException { get; set; }
        public DiscoveryRunCandidate? RestoredCandidate { get; set; }
        public DiscoveryRunQuery? LastListQuery { get; private set; }
        public DiscoveryRunCandidateQuery? LastCandidateQuery { get; private set; }
        public int? LastRestoreExpectedRevision { get; private set; }

        public Task<DiscoveryRun> CreateAsync(ProductNormaliser.Application.Sources.CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedRun ?? throw new InvalidOperationException("No run configured."));

        public Task<DiscoveryRun> CreateScheduledAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedRun ?? throw new InvalidOperationException("No run configured."));

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedRun is not null && string.Equals(CreatedRun.RunId, runId, StringComparison.OrdinalIgnoreCase) ? CreatedRun : null);

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
        {
            LastListQuery = query;
            return Task.FromResult(new DiscoveryRunPage
            {
                Items = CreatedRun is null ? [] : [CreatedRun],
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = CreatedRun is null ? 0 : 1
            });
        }

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(RestoredCandidate is null ? [] : [RestoredCandidate]);

        public Task<DiscoveryRunCandidatePage> QueryCandidatesAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default)
        {
            LastCandidateQuery = query;

            return Task.FromResult(new DiscoveryRunCandidatePage
            {
                Items = RestoredCandidate is null ? [] : [RestoredCandidate],
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = RestoredCandidate is null ? 0 : 1,
                Summary = new DiscoveryRunCandidateRunSummary
                {
                    RunCandidateCount = RestoredCandidate is null ? 0 : 1,
                    ActiveCandidateCount = RestoredCandidate is null ? 0 : 1,
                    ArchivedCandidateCount = 0,
                    LlmMeasuredCandidateCount = RestoredCandidate is null ? 0 : 1,
                    LlmBudgetProbeCappedCandidateCount = RestoredCandidate is null ? 0 : 1,
                    ProbeTimeoutCandidateCount = 0,
                    RepresentativePageFetchFailureCandidateCount = 0,
                    RepresentativeCategoryFetchFailureCount = 0,
                    RepresentativeProductFetchFailureCount = 0,
                    LlmTimeoutCandidateCount = 0,
                    AverageLlmBudgetMs = RestoredCandidate is null ? null : 200,
                    AverageLlmBudgetUtilizationPercent = RestoredCandidate is null ? null : 50m,
                    AutoAcceptBlockers =
                    [
                        new DiscoveryRunCandidateBlockerSummary
                        {
                            Code = "duplicate_risk_high",
                            Label = "Duplicate risk was too high",
                            Count = 1
                        }
                    ]
                }
            });
        }

        public Task<DiscoveryRun?> PauseAsync(string runId, CancellationToken cancellationToken = default)
            => PauseException is null ? Task.FromResult(CreatedRun) : Task.FromException<DiscoveryRun?>(PauseException);

        public Task<DiscoveryRun?> ResumeAsync(string runId, CancellationToken cancellationToken = default) => Task.FromResult(CreatedRun);
        public Task<DiscoveryRun?> StopAsync(string runId, CancellationToken cancellationToken = default) => Task.FromResult(CreatedRun);
        public Task<DiscoveryRunCandidate?> AcceptCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => Task.FromResult<DiscoveryRunCandidate?>(null);
        public Task<DiscoveryRunCandidate?> DismissCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => Task.FromResult<DiscoveryRunCandidate?>(null);
        public Task<DiscoveryRunCandidate?> RestoreCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
        {
            LastRestoreExpectedRevision = expectedRevision;
            return Task.FromResult(RestoredCandidate);
        }
    }
}
