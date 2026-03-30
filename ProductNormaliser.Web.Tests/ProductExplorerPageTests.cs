using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class ProductExplorerPageTests
{
    [Test]
    public async Task ProductsIndex_OnGetAsync_ForwardsFilterStateToApi()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            ProductPage = new ProductListResponseDto { Page = 2, PageSize = 12, TotalCount = 0, TotalPages = 0 }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            CategoryKey = "tv",
            Search = "sony",
            MinSourceCount = 3,
            Freshness = "stale",
            ConflictStatus = "with_conflicts",
            CompletenessStatus = "partial",
            PageNumber = 2
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastProductQuery, Is.Not.Null);
            Assert.That(client.LastProductQuery!.CategoryKey, Is.EqualTo("tv"));
            Assert.That(client.LastProductQuery.Search, Is.EqualTo("sony"));
            Assert.That(client.LastProductQuery.MinSourceCount, Is.EqualTo(3));
            Assert.That(client.LastProductQuery.Freshness, Is.EqualTo("stale"));
            Assert.That(client.LastProductQuery.ConflictStatus, Is.EqualTo("with_conflicts"));
            Assert.That(client.LastProductQuery.CompletenessStatus, Is.EqualTo("partial"));
            Assert.That(model.Pagination.RouteValues["search"], Is.EqualTo("sony"));
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetAsync_LoadsSearchResultsForAnalysisView()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            ProductPage = new ProductListResponseDto
            {
                Items =
                [
                    new ProductSummaryDto
                    {
                        Id = "canon-1",
                        CategoryKey = "tv",
                        Brand = "Sony",
                        ModelNumber = "XR-55A80L",
                        DisplayName = "Sony Bravia XR",
                        SourceCount = 4,
                        EvidenceCount = 14,
                        AttributeCount = 10,
                        HasConflict = true,
                        ConflictAttributeCount = 2,
                        CompletenessScore = 0.75m,
                        CompletenessStatus = "partial",
                        FreshnessStatus = "stale",
                        FreshnessAgeDays = 42,
                        KeyAttributes = [new ProductKeyAttributeDto { AttributeKey = "panel_type", DisplayName = "Panel Type", Value = "OLED" }],
                        UpdatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc)
                    }
                ],
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            Search = "bravia"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastProductQuery!.Search, Is.EqualTo("bravia"));
            Assert.That(model.Products.Items, Has.Count.EqualTo(1));
            Assert.That(model.ProductsWithConflicts, Is.EqualTo(1));
            Assert.That(model.StaleProducts, Is.EqualTo(1));
            Assert.That(model.AverageCompleteness, Is.EqualTo(0.75m));
        });
    }

    [Test]
    public async Task ProductsIndex_OnPostSaveViewAsync_SavesCurrentFilterStateAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            SavedAnalystWorkflow = new AnalystWorkflowDto
            {
                Id = "workflow_products_1",
                Name = "Sony stale queue",
                WorkflowType = AnalystWorkspacePresentation.WorkflowTypeProductFilters,
                RoutePath = "/Products/Index",
                PrimaryCategoryKey = "tv",
                SelectedCategoryKeys = ["tv"],
                State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["category"] = "tv",
                    ["search"] = "sony",
                    ["freshness"] = "stale"
                }
            }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            CategoryKey = "tv",
            Search = "sony",
            Freshness = "stale",
            ConflictStatus = "with_conflicts",
            SaveViewName = "Sony stale queue",
            SaveViewDescription = "Daily stale review"
        };

        var result = await model.OnPostSaveViewAsync(AnalystWorkspacePresentation.WorkflowTypeProductFilters, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastSavedAnalystWorkflowRequest, Is.Not.Null);
            Assert.That(client.LastSavedAnalystWorkflowRequest!.WorkflowType, Is.EqualTo(AnalystWorkspacePresentation.WorkflowTypeProductFilters));
            Assert.That(client.LastSavedAnalystWorkflowRequest.State["search"], Is.EqualTo("sony"));
            Assert.That(client.LastSavedAnalystWorkflowRequest.State["conflictStatus"], Is.EqualTo("with_conflicts"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.RouteValues! ["view"], Is.EqualTo("workflow_products_1"));
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetAsync_RestoresSavedViewState()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            AnalystWorkflows =
            [
                new AnalystWorkflowDto
                {
                    Id = "workflow_products_restore",
                    Name = "Conflict triage",
                    WorkflowType = AnalystWorkspacePresentation.WorkflowTypeProductFilters,
                    RoutePath = "/Products/Index",
                    PrimaryCategoryKey = "tv",
                    SelectedCategoryKeys = ["tv"],
                    State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["category"] = "tv",
                        ["search"] = "sony",
                        ["minSourceCount"] = "3",
                        ["freshness"] = "stale",
                        ["conflictStatus"] = "with_conflicts",
                        ["completeness"] = "partial",
                        ["sort"] = "conflicts_desc",
                        ["page"] = "2"
                    }
                }
            ],
            ProductPage = new ProductListResponseDto { Page = 2, PageSize = 12, TotalPages = 1, TotalCount = 0 }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            SavedViewId = "workflow_products_restore"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.CategoryKey, Is.EqualTo("tv"));
            Assert.That(model.Search, Is.EqualTo("sony"));
            Assert.That(model.MinSourceCount, Is.EqualTo(3));
            Assert.That(model.Freshness, Is.EqualTo("stale"));
            Assert.That(model.ConflictStatus, Is.EqualTo("with_conflicts"));
            Assert.That(model.CompletenessStatus, Is.EqualTo("partial"));
            Assert.That(model.Sort, Is.EqualTo("conflicts_desc"));
            Assert.That(model.PageNumber, Is.EqualTo(2));
            Assert.That(client.LastProductQuery!.Search, Is.EqualTo("sony"));
        });
    }

    [Test]
    public async Task ProductsIndex_OnPostDeleteViewAsync_DeletesSavedView()
    {
        var client = new FakeAdminApiClient();
        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            CategoryKey = "tv",
            Search = "sony"
        };

        var result = await model.OnPostDeleteViewAsync("workflow_products_delete", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastDeletedAnalystWorkflowId, Is.EqualTo("workflow_products_delete"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.RouteValues!["view"], Is.Null);
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetAsync_WhenSavedViewCategoryMissing_ShowsWorkflowMessageAndFallsBack()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            AnalystWorkflows =
            [
                new AnalystWorkflowDto
                {
                    Id = "workflow_missing_category",
                    Name = "Old TV queue",
                    WorkflowType = AnalystWorkspacePresentation.WorkflowTypeProductFilters,
                    RoutePath = "/Products/Index",
                    PrimaryCategoryKey = "tv",
                    SelectedCategoryKeys = ["tv"],
                    State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["category"] = "tv",
                        ["search"] = "sony"
                    }
                }
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            SavedViewId = "workflow_missing_category"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(model.WorkflowMessage, Does.Contain("missing category 'tv'"));
            Assert.That(model.SavedWorkflows[0].HasMissingCategory, Is.True);
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetAsync_FiltersCategorySelectorToRolloutCategories()
    {
        var client = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "laptop", DisplayName = "Laptops", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "refrigerator", DisplayName = "Refrigerators", IsEnabled = false, CrawlSupportStatus = "Planned" }
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance);

        await model.OnGetAsync(CancellationToken.None);

        Assert.That(model.Categories.Select(category => category.CategoryKey), Is.EqualTo(new[] { "laptop", "monitor", "tv" }));
    }

    [Test]
    public async Task ProductDetails_OnGetAsync_ReturnsMissingProductErrorState()
    {
        var client = new FakeAdminApiClient();
        var model = new ProductNormaliser.Web.Pages.Products.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.DetailsModel>.Instance)
        {
            ProductId = "missing"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Product, Is.Null);
            Assert.That(model.ErrorMessage, Is.EqualTo("Product 'missing' was not found."));
        });
    }

    [Test]
    public async Task ProductDetails_OnGetAsync_ShowsApiErrorState()
    {
        var client = new FakeThrowingProductClient();
        var model = new ProductNormaliser.Web.Pages.Products.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.DetailsModel>.Instance)
        {
            ProductId = "canon-1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.That(model.ErrorMessage, Is.EqualTo("Admin API is unavailable."));
    }

    [Test]
    public async Task ProductDetails_OnPostSaveNoteAsync_SavesProductNote()
    {
        var client = new FakeAdminApiClient();
        var model = new ProductNormaliser.Web.Pages.Products.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.DetailsModel>.Instance)
        {
            ProductId = "canon-1",
            NoteInput = new ProductNormaliser.Web.Pages.Products.DetailsModel.AnalystNoteInput
            {
                Title = "Check pricing",
                Content = "Revisit price drift after the next crawl."
            }
        };

        var result = await model.OnPostSaveNoteAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastSavedAnalystNoteRequest, Is.Not.Null);
            Assert.That(client.LastSavedAnalystNoteRequest!.TargetType, Is.EqualTo("product"));
            Assert.That(client.LastSavedAnalystNoteRequest.TargetId, Is.EqualTo("canon-1"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetExportAsync_ExportsFilteredProductsCsv()
    {
        var client = new FakeAdminApiClient
        {
            ProductPage = new ProductListResponseDto
            {
                Items =
                [
                    new ProductSummaryDto
                    {
                        Id = "canon-1",
                        DisplayName = "Sony Bravia",
                        CategoryKey = "tv",
                        Brand = "Sony",
                        ModelNumber = "A80L",
                        Gtin = "123",
                        SourceCount = 3,
                        EvidenceCount = 12,
                        ConflictAttributeCount = 2,
                        HasConflict = true,
                        CompletenessScore = 0.75m,
                        CompletenessStatus = "partial",
                        FreshnessStatus = "stale",
                        FreshnessAgeDays = 42,
                        UpdatedUtc = new DateTime(2026, 3, 23, 8, 0, 0, DateTimeKind.Utc)
                    }
                ],
                Page = 1,
                PageSize = 1,
                TotalCount = 1,
                TotalPages = 1
            }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            CategoryKey = "tv",
            Search = "sony",
            Freshness = "stale",
            ConflictStatus = "with_conflicts"
        };

        var result = await model.OnGetExportAsync(CancellationToken.None);
        var csv = Encoding.UTF8.GetString(result.FileContents);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastProductQuery, Is.Not.Null);
            Assert.That(client.LastProductQuery!.Search, Is.EqualTo("sony"));
            Assert.That(client.LastProductQuery.Freshness, Is.EqualTo("stale"));
            Assert.That(csv, Does.Contain("ProductId,DisplayName,CategoryKey"));
            Assert.That(csv, Does.Contain("canon-1,Sony Bravia,tv,Sony,A80L,123,3,12,2,true,0.75,partial,stale,42"));
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetExportAsync_WhenEmpty_ReturnsHeaderOnlyCsv()
    {
        var client = new FakeAdminApiClient
        {
            ProductPage = new ProductListResponseDto { Items = [], Page = 1, PageSize = 12, TotalCount = 0, TotalPages = 0 }
        };
        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance);

        var result = await model.OnGetExportAsync(CancellationToken.None);
        var csv = Encoding.UTF8.GetString(result.FileContents).Trim();

        Assert.That(csv, Is.EqualTo("ProductId,DisplayName,CategoryKey,Brand,ModelNumber,Gtin,SourceCount,EvidenceCount,ConflictAttributeCount,HasConflict,CompletenessScore,CompletenessStatus,FreshnessStatus,FreshnessAgeDays,UpdatedUtc"));
    }

    [Test]
    public void ProductExplorerPresentation_ReturnsRenderingBadges()
    {
        var freshness = ProductExplorerPresentation.GetFreshnessBadge("stale", 42);
        var completeness = ProductExplorerPresentation.GetCompletenessBadge(0.42m, "sparse");
        var conflict = ProductExplorerPresentation.GetConflictBadge(true, 3);

        Assert.Multiple(() =>
        {
            Assert.That(freshness.Text, Does.Contain("42d"));
            Assert.That(freshness.Tone, Is.EqualTo("danger"));
            Assert.That(completeness.Text, Is.EqualTo("Sparse 42%"));
            Assert.That(conflict.Text, Is.EqualTo("3 conflicts"));
        });
    }

    private sealed class FakeThrowingProductClient : IProductNormaliserAdminApiClient
    {
        public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AnalystWorkflowDto>> GetAnalystWorkflowsAsync(string? workflowType = null, string? routePath = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AnalystWorkflowDto?> GetAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AnalystWorkflowDto> SaveAnalystWorkflowAsync(UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AnalystNoteDto?> GetAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default) => Task.FromResult<AnalystNoteDto?>(null);
        public Task<AnalystNoteDto> SaveAnalystNoteAsync(UpsertAnalystNoteRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CategorySchemaDto> UpdateCategorySchemaAsync(string categoryKey, UpdateCategorySchemaRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceOnboardingAutomationSettingsDto> GetSourceOnboardingAutomationSettingsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceCandidateDiscoveryResponseDto> DiscoverSourceCandidatesAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunDto> CreateDiscoveryRunAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunPageDto> GetDiscoveryRunsAsync(string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunDto?> GetDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunCandidatePageDto> GetDiscoveryRunCandidatesAsync(string runId, DiscoveryRunCandidateQueryDto? query = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunDto> PauseDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunDto> ResumeDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunDto> StopDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunCandidateDto> AcceptDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunCandidateDto> DismissDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscoveryRunCandidateDto> RestoreDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductListResponseDto> GetProductsAsync(ProductListQueryDto? query = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default) => Task.FromException<ProductDetailDto?>(new AdminApiException("Admin API is unavailable."));
        public Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default) => Task.FromException<IReadOnlyList<ProductChangeEventDto>>(new AdminApiException("Admin API is unavailable."));
        public Task<DetailedCoverageResponseDto> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MergeInsightsResponseDto> GetMergeInsightsAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}