using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

public sealed class SourceCandidateDiscoveryServiceTests
{
    [Test]
    public async Task DiscoverAsync_ReturnsScoredCandidates_WithDuplicateAndGovernanceFlags()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "currys_uk",
            DisplayName = "Currys",
            BaseUrl = "https://www.currys.co.uk/",
            Host = "www.currys.co.uk",
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var searchProvider = new FakeSourceCandidateSearchProvider(
            new SourceCandidateSearchResult
            {
                CandidateKey = "currys_co_uk",
                DisplayName = "Currys",
                BaseUrl = "https://www.currys.co.uk/",
                Host = "www.currys.co.uk",
                CandidateType = "retailer",
                MatchedCategoryKeys = ["tv"],
                MatchedBrandHints = ["Samsung"],
                SearchReasons = ["Matched retailer search results."]
            },
            new SourceCandidateSearchResult
            {
                CandidateKey = "blocked_shop",
                DisplayName = "Blocked Shop",
                BaseUrl = "https://blocked.example/",
                Host = "blocked.example",
                CandidateType = "manufacturer",
                MatchedCategoryKeys = ["tv"],
                SearchReasons = ["Matched manufacturer search results."]
            });
        var probeService = new FakeSourceCandidateProbeService(
            new Dictionary<string, SourceCandidateProbeResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["www.currys.co.uk"] = new SourceCandidateProbeResult
                {
                    HomePageReachable = true,
                    RobotsTxtReachable = true,
                    SitemapDetected = true,
                    SitemapUrls = ["https://www.currys.co.uk/sitemap.xml"],
                    CategoryRelevanceScore = 12m
                },
                ["blocked.example"] = new SourceCandidateProbeResult
                {
                    HomePageReachable = true,
                    CategoryRelevanceScore = 8m
                }
            });
        var service = CreateService(
            store,
            new FakeCategoryMetadataService(CreateCategory("tv")),
            searchProvider,
            probeService,
            new BlockingGovernanceService("blocked.example"));

        var result = await service.DiscoverAsync(new DiscoverSourceCandidatesRequest
        {
            CategoryKeys = ["tv"],
            BrandHints = ["Samsung"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Candidates, Has.Count.EqualTo(2));
            Assert.That(result.Candidates.Select(candidate => candidate.DisplayName), Is.EqualTo(new[] { "Currys", "Blocked Shop" }));

            var duplicateCandidate = result.Candidates[0];
            Assert.That(duplicateCandidate.AlreadyRegistered, Is.True);
            Assert.That(duplicateCandidate.DuplicateSourceIds, Is.EqualTo(new[] { "currys_uk" }));
            Assert.That(duplicateCandidate.DuplicateSourceDisplayNames, Is.EqualTo(new[] { "Currys" }));
            Assert.That(duplicateCandidate.AllowedByGovernance, Is.True);
            Assert.That(duplicateCandidate.ConfidenceScore, Is.EqualTo(37m));
            Assert.That(duplicateCandidate.Reasons.Select(reason => reason.Code), Does.Contain("duplicate"));
            Assert.That(duplicateCandidate.Reasons.Select(reason => reason.Code), Does.Contain("sitemap"));

            var blockedCandidate = result.Candidates[1];
            Assert.That(blockedCandidate.AllowedByGovernance, Is.False);
            Assert.That(blockedCandidate.GovernanceWarning, Does.Contain("blocked by crawl governance rules"));
            Assert.That(blockedCandidate.ConfidenceScore, Is.EqualTo(10m));
            Assert.That(blockedCandidate.Reasons.Select(reason => reason.Code), Does.Contain("governance"));
        });
    }

    [Test]
    public void DiscoverAsync_RejectsUnknownCategoryKeys()
    {
        var searchProvider = new FakeSourceCandidateSearchProvider();
        var service = CreateService(
            new FakeCrawlSourceStore(),
            new FakeCategoryMetadataService(CreateCategory("tv")),
            searchProvider,
            new FakeSourceCandidateProbeService(),
            new PermissiveCrawlGovernanceService());

        var action = async () => await service.DiscoverAsync(new DiscoverSourceCandidatesRequest
        {
            CategoryKeys = ["tv", "smartwatch"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(action, Throws.ArgumentException.With.Message.Contain("Unknown category keys: smartwatch."));
            Assert.That(searchProvider.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task DiscoverAsync_NormalizesInputs_AndAppliesMaxCandidateLimit()
    {
        var searchProvider = new FakeSourceCandidateSearchProvider(
            new SourceCandidateSearchResult
            {
                CandidateKey = "gamma",
                DisplayName = "Gamma",
                BaseUrl = "https://gamma.example/",
                Host = "gamma.example",
                CandidateType = "retailer",
                MatchedCategoryKeys = ["tv"],
                SearchReasons = ["Gamma result"]
            },
            new SourceCandidateSearchResult
            {
                CandidateKey = "alpha",
                DisplayName = "Alpha",
                BaseUrl = "https://alpha.example/",
                Host = "alpha.example",
                CandidateType = "retailer",
                MatchedCategoryKeys = ["tv"],
                SearchReasons = ["Alpha result"]
            },
            new SourceCandidateSearchResult
            {
                CandidateKey = "beta",
                DisplayName = "Beta",
                BaseUrl = "https://beta.example/",
                Host = "beta.example",
                CandidateType = "retailer",
                MatchedCategoryKeys = ["tv"],
                SearchReasons = ["Beta result"]
            });
        var probeService = new FakeSourceCandidateProbeService(
            new Dictionary<string, SourceCandidateProbeResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["gamma.example"] = new SourceCandidateProbeResult { CategoryRelevanceScore = 30m },
                ["alpha.example"] = new SourceCandidateProbeResult { CategoryRelevanceScore = 20m },
                ["beta.example"] = new SourceCandidateProbeResult { CategoryRelevanceScore = 10m }
            });
        var service = CreateService(
            new FakeCrawlSourceStore(),
            new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("laptop")),
            searchProvider,
            probeService,
            new PermissiveCrawlGovernanceService());

        var result = await service.DiscoverAsync(new DiscoverSourceCandidatesRequest
        {
            CategoryKeys = [" tv ", "laptop", "tv"],
            Locale = " en-GB ",
            Market = " UK ",
            BrandHints = ["Samsung", " samsung ", " LG "],
            MaxCandidates = 2
        });

        Assert.Multiple(() =>
        {
            Assert.That(searchProvider.LastRequest, Is.Not.Null);
            Assert.That(searchProvider.LastRequest!.CategoryKeys, Is.EqualTo(new[] { "laptop", "tv" }));
            Assert.That(searchProvider.LastRequest.Locale, Is.EqualTo("en-GB"));
            Assert.That(searchProvider.LastRequest.Market, Is.EqualTo("UK"));
            Assert.That(searchProvider.LastRequest.BrandHints, Is.EqualTo(new[] { "LG", "Samsung" }));
            Assert.That(searchProvider.LastRequest.MaxCandidates, Is.EqualTo(2));

            Assert.That(probeService.CategoryKeysByHost["gamma.example"], Is.EqualTo(new[] { "laptop", "tv" }));
            Assert.That(result.RequestedCategoryKeys, Is.EqualTo(new[] { "laptop", "tv" }));
            Assert.That(result.Locale, Is.EqualTo("en-GB"));
            Assert.That(result.Market, Is.EqualTo("UK"));
            Assert.That(result.BrandHints, Is.EqualTo(new[] { "LG", "Samsung" }));
            Assert.That(result.Candidates.Select(candidate => candidate.DisplayName), Is.EqualTo(new[] { "Gamma", "Alpha" }));
        });
    }

    [Test]
    public async Task DiscoverAsync_UsesSearchResults_ToBuildCandidatesWithoutProbeSignals()
    {
        var service = CreateService(
            new FakeCrawlSourceStore(),
            new FakeCategoryMetadataService(CreateCategory("tv")),
            new FakeSourceCandidateSearchProvider(
                new SourceCandidateSearchResult
                {
                    CandidateKey = "ao_example",
                    DisplayName = "AO Example",
                    BaseUrl = "https://ao.example/",
                    Host = "ao.example",
                    CandidateType = "retailer",
                    MatchedCategoryKeys = ["tv"],
                    SearchReasons = ["Matched retailer search results."]
                }),
            new FakeSourceCandidateProbeService(),
            new PermissiveCrawlGovernanceService());

        var result = await service.DiscoverAsync(new DiscoverSourceCandidatesRequest
        {
            CategoryKeys = ["tv"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Candidates, Has.Count.EqualTo(1));
            Assert.That(result.Candidates[0].DisplayName, Is.EqualTo("AO Example"));
            Assert.That(result.Candidates[0].ConfidenceScore, Is.EqualTo(15m));
            Assert.That(result.Candidates[0].AllowedByGovernance, Is.True);
            Assert.That(result.Candidates[0].Reasons.Select(reason => reason.Code), Is.EqualTo(new[] { "search_match" }));
            Assert.That(result.Candidates[0].Probe.RobotsTxtReachable, Is.False);
            Assert.That(result.Candidates[0].Probe.SitemapDetected, Is.False);
        });
    }

    private static SourceCandidateDiscoveryService CreateService(
        FakeCrawlSourceStore store,
        FakeCategoryMetadataService categoryService,
        FakeSourceCandidateSearchProvider searchProvider,
        FakeSourceCandidateProbeService probeService,
        ICrawlGovernanceService governanceService)
    {
        return new SourceCandidateDiscoveryService(
            store,
            categoryService,
            governanceService,
            searchProvider,
            probeService);
    }

    private static CategoryMetadata CreateCategory(string key)
    {
        return new CategoryMetadata
        {
            CategoryKey = key,
            DisplayName = key,
            FamilyKey = "family",
            FamilyDisplayName = "Family",
            IconKey = key,
            CrawlSupportStatus = CrawlSupportStatus.Supported,
            SchemaCompletenessScore = 1m,
            IsEnabled = true
        };
    }

    private sealed class FakeCrawlSourceStore(params CrawlSource[] sources) : ICrawlSourceStore
    {
        private readonly List<CrawlSource> items = sources.ToList();

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlSource>>(items.ToArray());

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(source => string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase)));

        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
        {
            items.RemoveAll(existing => string.Equals(existing.Id, source.Id, StringComparison.OrdinalIgnoreCase));
            items.Add(source);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCategoryMetadataService(params CategoryMetadata[] categories) : ICategoryMetadataService
    {
        private readonly List<CategoryMetadata> items = categories.ToList();

        public Task EnsureDefaultsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
        {
            var result = enabledOnly ? items.Where(category => category.IsEnabled).ToArray() : items.ToArray();
            return Task.FromResult<IReadOnlyList<CategoryMetadata>>(result);
        }

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)));

        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            items.RemoveAll(category => string.Equals(category.CategoryKey, categoryMetadata.CategoryKey, StringComparison.OrdinalIgnoreCase));
            items.Add(categoryMetadata);
            return Task.FromResult(categoryMetadata);
        }
    }

    private sealed class FakeSourceCandidateSearchProvider(params SourceCandidateSearchResult[] results) : ISourceCandidateSearchProvider
    {
        private readonly IReadOnlyList<SourceCandidateSearchResult> results = results;

        public int CallCount { get; private set; }

        public DiscoverSourceCandidatesRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<SourceCandidateSearchResult>> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(results);
        }
    }

    private sealed class FakeSourceCandidateProbeService : ISourceCandidateProbeService
    {
        private readonly IReadOnlyDictionary<string, SourceCandidateProbeResult> resultsByHost;

        public FakeSourceCandidateProbeService()
            : this(new Dictionary<string, SourceCandidateProbeResult>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public FakeSourceCandidateProbeService(IReadOnlyDictionary<string, SourceCandidateProbeResult> resultsByHost)
        {
            this.resultsByHost = resultsByHost;
        }

        public Dictionary<string, IReadOnlyCollection<string>> CategoryKeysByHost { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<SourceCandidateProbeResult> ProbeAsync(SourceCandidateSearchResult candidate, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
        {
            CategoryKeysByHost[candidate.Host] = categoryKeys.ToArray();
            return Task.FromResult(resultsByHost.TryGetValue(candidate.Host, out var result) ? result : new SourceCandidateProbeResult());
        }
    }

    private sealed class PermissiveCrawlGovernanceService : ICrawlGovernanceService
    {
        public void ValidateSourceBaseUrl(string baseUrl, string parameterName)
        {
        }

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<CrawlJobTargetDescriptor> targets, string parameterName)
        {
        }
    }

    private sealed class BlockingGovernanceService(string blockedHost) : ICrawlGovernanceService
    {
        public void ValidateSourceBaseUrl(string baseUrl, string parameterName)
        {
            if (string.Equals(new Uri(baseUrl).Host, blockedHost, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Source '{baseUrl}' is blocked by crawl governance rules.", parameterName);
            }
        }

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<CrawlJobTargetDescriptor> targets, string parameterName)
        {
        }
    }
}