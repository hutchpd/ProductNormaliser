using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class DiscoveryRunsControllerTests
{
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

        var result = await controller.GetCandidates("missing", CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
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
            Probe = new DiscoveryRunCandidateProbe(),
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
        public int? LastRestoreExpectedRevision { get; private set; }

        public Task<DiscoveryRun> CreateAsync(ProductNormaliser.Application.Sources.CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedRun ?? throw new InvalidOperationException("No run configured."));

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedRun is not null && string.Equals(CreatedRun.RunId, runId, StringComparison.OrdinalIgnoreCase) ? CreatedRun : null);

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new DiscoveryRunPage
            {
                Items = CreatedRun is null ? [] : [CreatedRun],
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = CreatedRun is null ? 0 : 1
            });

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(RestoredCandidate is null ? [] : [RestoredCandidate]);

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
