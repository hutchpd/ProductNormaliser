using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

public sealed class SourceManagementServiceTests
{
    [Test]
    public async Task RegisterAsync_CreatesNormalisedSource()
    {
        var store = new FakeCrawlSourceStore();
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("refrigerator")));

        var result = await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "Retailer One",
            DisplayName = "Retailer One",
            BaseUrl = "https://www.retailer-one.example/",
            AllowedMarkets = ["uk", "ie"],
            PreferredLocale = " en-GB ",
            SupportedCategoryKeys = ["tv", "refrigerator"],
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                AllowedMarkets = ["UK", "IE"],
                PreferredLocale = "en-GB",
                CategoryEntryPages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tv"] = ["/televisions", "https://www.retailer-one.example/televisions/"],
                    ["refrigerator"] = ["/cooling"]
                },
                SitemapHints = ["/sitemap.xml", "https://www.retailer-one.example/sitemap.xml"],
                AllowedHosts = ["media.retailer-one.example", "https://img.retailer-one.example/assets"],
                AllowedPathPrefixes = ["tv", "/tv", "https://www.retailer-one.example/product"],
                ExcludedPathPrefixes = ["/support/", "/support"],
                ProductUrlPatterns = ["/product/", " /p/ "],
                ListingUrlPatterns = ["/category/", " /department/ "],
                MaxDiscoveryDepth = 4,
                MaxUrlsPerRun = 750,
                SeedReseedIntervalHours = 12,
                MaxRetryCount = 2,
                RetryBackoffBaseMs = 1500,
                RetryBackoffMaxMs = 12000
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo("retailer_one"));
            Assert.That(result.Host, Is.EqualTo("www.retailer-one.example"));
            Assert.That(result.AllowedMarkets, Is.EqualTo(new[] { "IE", "UK" }));
            Assert.That(result.PreferredLocale, Is.EqualTo("en-GB"));
            Assert.That(result.SupportedCategoryKeys, Is.EqualTo(new[] { "refrigerator", "tv" }));
            Assert.That(result.DiscoveryProfile.AllowedMarkets, Is.EqualTo(new[] { "IE", "UK" }));
            Assert.That(result.DiscoveryProfile.PreferredLocale, Is.EqualTo("en-GB"));
            Assert.That(result.DiscoveryProfile.CategoryEntryPages["tv"], Is.EqualTo(new[] { "https://www.retailer-one.example/televisions" }));
            Assert.That(result.DiscoveryProfile.CategoryEntryPages["refrigerator"], Is.EqualTo(new[] { "https://www.retailer-one.example/cooling" }));
            Assert.That(result.DiscoveryProfile.SitemapHints, Is.EqualTo(new[] { "https://www.retailer-one.example/sitemap.xml" }));
            Assert.That(result.DiscoveryProfile.AllowedHosts, Is.EqualTo(new[] { "media.retailer-one.example", "img.retailer-one.example" }));
            Assert.That(result.DiscoveryProfile.AllowedPathPrefixes, Is.EqualTo(new[] { "/tv", "/product" }));
            Assert.That(result.DiscoveryProfile.ExcludedPathPrefixes, Is.EqualTo(new[] { "/support" }));
            Assert.That(result.DiscoveryProfile.ProductUrlPatterns, Is.EqualTo(new[] { "/product/", "/p/" }));
            Assert.That(result.DiscoveryProfile.ListingUrlPatterns, Is.EqualTo(new[] { "/category/", "/department/" }));
            Assert.That(result.DiscoveryProfile.MaxDiscoveryDepth, Is.EqualTo(4));
            Assert.That(result.DiscoveryProfile.MaxUrlsPerRun, Is.EqualTo(750));
            Assert.That(result.DiscoveryProfile.SeedReseedIntervalHours, Is.EqualTo(12));
            Assert.That(result.DiscoveryProfile.MaxRetryCount, Is.EqualTo(2));
            Assert.That(result.DiscoveryProfile.RetryBackoffBaseMs, Is.EqualTo(1500));
            Assert.That(result.DiscoveryProfile.RetryBackoffMaxMs, Is.EqualTo(12000));
            Assert.That(store.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void SetThrottlingAsync_RejectsRobotsBypass()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv")));

        var action = async () => await service.SetThrottlingAsync("alpha", new SourceThrottlingPolicy
        {
            MinDelayMs = 1000,
            MaxDelayMs = 2000,
            MaxConcurrentRequests = 1,
            RequestsPerMinute = 10,
            RespectRobotsTxt = false
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Robots.txt checks are mandatory"));
    }

    [Test]
    public void RegisterAsync_RejectsDiscoveryProfilesForUnsupportedCategories()
    {
        var service = CreateService(new FakeCrawlSourceStore(), new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("monitor")));

        var action = async () => await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            SupportedCategoryKeys = ["tv"],
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                CategoryEntryPages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["monitor"] = ["/monitors"]
                }
            }
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("supported categories"));
    }

    [Test]
    public async Task RegisterAsync_AppliesStartupDiscoveryDefaultsWhenProfileIsOmitted()
    {
        var store = new FakeCrawlSourceStore();
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("laptop")));

        var result = await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "northwind",
            DisplayName = "Northwind",
            BaseUrl = "https://www.northwind.example/",
            SupportedCategoryKeys = ["tv", "laptop"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.AllowedMarkets, Is.EqualTo(new[] { "UK" }));
            Assert.That(result.PreferredLocale, Is.EqualTo("en-GB"));
            Assert.That(result.AutomationPolicy.Mode, Is.EqualTo("operator_assisted"));
            Assert.That(result.DiscoveryProfile.SitemapHints, Is.Not.Empty);
            Assert.That(result.DiscoveryProfile.AllowedMarkets, Is.EqualTo(new[] { "UK" }));
            Assert.That(result.DiscoveryProfile.PreferredLocale, Is.EqualTo("en-GB"));
            Assert.That(result.DiscoveryProfile.CategoryEntryPages["tv"], Does.Contain("https://www.northwind.example/tv"));
            Assert.That(result.DiscoveryProfile.CategoryEntryPages["laptop"], Does.Contain("https://www.northwind.example/laptops"));
            Assert.That(result.DiscoveryProfile.ProductUrlPatterns, Does.Contain("/product/"));
            Assert.That(result.DiscoveryProfile.ExcludedPathPrefixes, Does.Contain("/support"));
            Assert.That(result.DiscoveryProfile.SeedReseedIntervalHours, Is.EqualTo(24));
        });
    }

    [Test]
    public void RegisterAsync_RejectsNonPositiveSeedReseedInterval()
    {
        var service = CreateService(new FakeCrawlSourceStore(), new FakeCategoryMetadataService(CreateCategory("tv")));

        var action = async () => await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            SupportedCategoryKeys = ["tv"],
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                SeedReseedIntervalHours = 0
            }
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Seed reseed interval must be greater than zero hours."));
    }

    [Test]
    public async Task UpdateAsync_UpdatesAllowedMarketsAndPreferredLocale()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            AutomationPolicy = new SourceAutomationPolicy
            {
                Mode = "operator_assisted"
            },
            SupportedCategoryKeys = ["tv"],
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                AllowedMarkets = ["UK"],
                PreferredLocale = "en-GB"
            },
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv")));

        var result = await service.UpdateAsync("alpha", new CrawlSourceUpdate
        {
            DisplayName = "Alpha UK",
            BaseUrl = "https://alpha.example/uk/",
            Description = "Updated",
            AllowedMarkets = ["UK", "IE"],
            PreferredLocale = "en-IE",
            AutomationPolicy = new SourceAutomationPolicy
            {
                Mode = "suggest_accept"
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.AllowedMarkets, Is.EqualTo(new[] { "IE", "UK" }));
            Assert.That(result.PreferredLocale, Is.EqualTo("en-IE"));
            Assert.That(result.AutomationPolicy.Mode, Is.EqualTo("suggest_accept"));
        });
    }

    [Test]
    public void RegisterAsync_RejectsUnknownCategories()
    {
        var service = CreateService(new FakeCrawlSourceStore(), new FakeCategoryMetadataService(CreateCategory("tv")));

        var action = async () => await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            SupportedCategoryKeys = ["tv", "smartwatch"]
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Unknown category keys"));
    }

    [Test]
    public async Task AssignCategoriesAsync_UpdatesSupportedCategories()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            SupportedCategoryKeys = ["tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("refrigerator")));

        var result = await service.AssignCategoriesAsync("alpha", ["refrigerator"]);

        Assert.That(result.SupportedCategoryKeys, Is.EqualTo(new[] { "refrigerator" }));
    }

    [Test]
    public void SetThrottlingAsync_RejectsInvalidPolicy()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv")));

        var action = async () => await service.SetThrottlingAsync("alpha", new SourceThrottlingPolicy
        {
            MinDelayMs = 2000,
            MaxDelayMs = 1000,
            MaxConcurrentRequests = 1,
            RequestsPerMinute = 10,
            RespectRobotsTxt = true
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Maximum delay"));
    }

    [Test]
    public async Task EnableAndDisableAsync_ToggleSourceState()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = false,
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var audit = new RecordingAuditService();
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv")), auditService: audit);

        var enabled = await service.EnableAsync("alpha");
        var enabledState = enabled.IsEnabled;
        var disabled = await service.DisableAsync("alpha");

        Assert.Multiple(() =>
        {
            Assert.That(enabledState, Is.True);
            Assert.That(disabled.IsEnabled, Is.False);
            Assert.That(audit.Entries.Select(entry => entry.Action), Is.EqualTo(new[] { ManagementAuditActions.SourceEnabled, ManagementAuditActions.SourceDisabled }));
        });
    }

    [Test]
    public async Task AssignCategoriesAsync_CreatesAuditEntry()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            SupportedCategoryKeys = ["tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var audit = new RecordingAuditService();
        var service = CreateService(store, new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("monitor")), auditService: audit);

        await service.AssignCategoriesAsync("alpha", ["monitor"]);

        Assert.Multiple(() =>
        {
            Assert.That(audit.Entries, Has.Count.EqualTo(1));
            Assert.That(audit.Entries[0].Action, Is.EqualTo(ManagementAuditActions.SourceCategoriesChanged));
            Assert.That(audit.Entries[0].Details["previousCategories"], Is.EqualTo("tv"));
            Assert.That(audit.Entries[0].Details["updatedCategories"], Is.EqualTo("monitor"));
        });
    }

    [Test]
    public void RegisterAsync_RejectsBlockedDomains()
    {
        var service = new SourceManagementService(
            new FakeCrawlSourceStore(),
            new FakeCategoryMetadataService(CreateCategory("tv")),
            new FixedCrawlGovernanceService(new CrawlGovernanceOptions
            {
                BlockedDomains = ["blocked.example"]
            }),
            new RecordingAuditService());

        var action = async () => await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "blocked",
            DisplayName = "Blocked",
            BaseUrl = "https://blocked.example",
            SupportedCategoryKeys = ["tv"]
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("blocked by crawl governance rules"));
    }

    [Test]
    public void RegisterAsync_RejectsPrivateNetworkTargets()
    {
        var service = new SourceManagementService(
            new FakeCrawlSourceStore(),
            new FakeCategoryMetadataService(CreateCategory("tv")),
            new FixedCrawlGovernanceService(new CrawlGovernanceOptions
            {
                AllowPrivateNetworkTargets = false
            }),
            new RecordingAuditService());

        var action = async () => await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "private",
            DisplayName = "Private",
            BaseUrl = "https://localhost:8443",
            SupportedCategoryKeys = ["tv"]
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("local or private-network target"));
    }

    private static SourceManagementService CreateService(
        FakeCrawlSourceStore store,
        FakeCategoryMetadataService categoryService,
        ICrawlGovernanceService? governanceService = null,
        IManagementAuditService? auditService = null)
    {
        return new SourceManagementService(
            store,
            categoryService,
            governanceService ?? new PermissiveCrawlGovernanceService(),
            auditService ?? new RecordingAuditService());
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
        public List<CrawlSource> Items { get; } = sources.ToList();

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlSource>>(Items.ToArray());
        }

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(source => string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase)));
        }

        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(existing => string.Equals(existing.Id, source.Id, StringComparison.OrdinalIgnoreCase));
            Items.Add(source);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCategoryMetadataService(params CategoryMetadata[] categories) : ICategoryMetadataService
    {
        private readonly List<CategoryMetadata> items = categories.ToList();

        public Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
        {
            var result = enabledOnly ? items.Where(category => category.IsEnabled).ToArray() : items.ToArray();
            return Task.FromResult<IReadOnlyList<CategoryMetadata>>(result);
        }

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            items.RemoveAll(category => string.Equals(category.CategoryKey, categoryMetadata.CategoryKey, StringComparison.OrdinalIgnoreCase));
            items.Add(categoryMetadata);
            return Task.FromResult(categoryMetadata);
        }
    }

    private sealed class RecordingAuditService : IManagementAuditService
    {
        public List<ManagementAuditEntry> Entries { get; } = [];

        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? details = null, CancellationToken cancellationToken = default)
        {
            Entries.Add(new ManagementAuditEntry
            {
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details is null ? [] : new Dictionary<string, string>(details, StringComparer.OrdinalIgnoreCase)
            });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagementAuditEntry>>(Entries.Take(take).ToArray());
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

    private sealed class FixedCrawlGovernanceService(CrawlGovernanceOptions options) : ICrawlGovernanceService
    {
        private readonly CrawlGovernanceService inner = new(Microsoft.Extensions.Options.Options.Create(options));

        public void ValidateSourceBaseUrl(string baseUrl, string parameterName) => inner.ValidateSourceBaseUrl(baseUrl, parameterName);

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<CrawlJobTargetDescriptor> targets, string parameterName)
            => inner.ValidateCrawlRequest(requestType, categories, sources, productIds, targets, parameterName);
    }
}