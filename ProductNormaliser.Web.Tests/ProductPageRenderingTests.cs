using Microsoft.AspNetCore.Mvc.Testing;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
public sealed class ProductPageRenderingTests
{
    [Test]
    public async Task ExplorerPage_RendersFilterStateAndAnalysisCards()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto
                {
                    CategoryKey = "tv",
                    DisplayName = "TVs",
                    IsEnabled = true,
                    CrawlSupportStatus = "Supported"
                }
            ],
            ProductPage = new ProductListResponseDto
            {
                Page = 2,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 2,
                Items =
                [
                    new ProductSummaryDto
                    {
                        Id = "prod_tv_oled_001",
                        CategoryKey = "tv",
                        Brand = "Northwind",
                        ModelNumber = "NW-55-OLED",
                        DisplayName = "Northwind OLED 55",
                        SourceCount = 3,
                        EvidenceCount = 7,
                        ConflictAttributeCount = 2,
                        HasConflict = true,
                        CompletenessScore = 0.42m,
                        CompletenessStatus = "partial",
                        FreshnessStatus = "stale",
                        FreshnessAgeDays = 48,
                        UpdatedUtc = new DateTime(2025, 1, 18, 9, 15, 0, DateTimeKind.Utc),
                        KeyAttributes =
                        [
                            new ProductKeyAttributeDto
                            {
                                AttributeKey = "resolution",
                                DisplayName = "Resolution",
                                Value = "4K"
                            },
                            new ProductKeyAttributeDto
                            {
                                AttributeKey = "hdmi_standard",
                                DisplayName = "HDMI",
                                Value = "HDMI 2.1",
                                HasConflict = true
                            }
                        ]
                    }
                ]
            }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Products?search=OLED&category=tv&minSourceCount=2&freshness=stale&conflictStatus=with_conflicts&completeness=partial&sort=stale&page=2");

        Assert.That(html, Does.Contain("Find products by quality signals"));
        Assert.That(html, Does.Contain("selected=\"selected\">Stalest first"));
        Assert.That(html, Does.Contain("Northwind OLED 55"));
        Assert.That(html, Does.Contain("Products with conflicts"));
        Assert.That(html, Does.Contain("HDMI 2.1"));
        Assert.That(html, Does.Contain("/Products/Details?productId=prod_tv_oled_001"));
        Assert.That(html, Does.Contain("sort=stale"));
    }

    [Test]
    public async Task ExplorerPage_RendersEnabledSupportedAndExperimentalCategoryOptions()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "laptop", DisplayName = "Laptops", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "tablet", DisplayName = "Tablets", IsEnabled = true, CrawlSupportStatus = "Experimental" },
                new CategoryMetadataDto { CategoryKey = "smartphone", DisplayName = "Smartphones", IsEnabled = true, CrawlSupportStatus = "Experimental" },
                new CategoryMetadataDto { CategoryKey = "headphones", DisplayName = "Headphones", IsEnabled = true, CrawlSupportStatus = "Experimental" },
                new CategoryMetadataDto { CategoryKey = "speakers", DisplayName = "Speakers", IsEnabled = true, CrawlSupportStatus = "Experimental" },
                new CategoryMetadataDto { CategoryKey = "refrigerator", DisplayName = "Refrigerators", IsEnabled = false, CrawlSupportStatus = "Planned" }
            ],
            ProductPage = new ProductListResponseDto { Page = 1, PageSize = 12, TotalCount = 0, TotalPages = 0 }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Products");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain(">TVs<"));
            Assert.That(html, Does.Contain(">Monitors<"));
            Assert.That(html, Does.Contain(">Laptops<"));
            Assert.That(html, Does.Contain(">Tablets<"));
            Assert.That(html, Does.Contain(">Smartphones<"));
            Assert.That(html, Does.Contain(">Headphones<"));
            Assert.That(html, Does.Contain(">Speakers<"));
            Assert.That(html, Does.Not.Contain(">Refrigerators<"));
        });
    }

    [Test]
    public async Task DetailPage_RendersExplainabilitySectionsAndReturnLinkState()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Product = new ProductDetailDto
            {
                Id = "prod_tv_oled_001",
                CategoryKey = "tv",
                Brand = "Northwind",
                ModelNumber = "NW-55-OLED",
                DisplayName = "Northwind OLED 55",
                CreatedUtc = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2025, 1, 18, 9, 15, 0, DateTimeKind.Utc),
                SourceCount = 2,
                EvidenceCount = 3,
                ConflictAttributeCount = 1,
                HasConflict = true,
                CompletenessScore = 0.67m,
                CompletenessStatus = "partial",
                FreshnessStatus = "aging",
                FreshnessAgeDays = 14,
                KeyAttributes =
                [
                    new ProductKeyAttributeDto
                    {
                        AttributeKey = "resolution",
                        DisplayName = "Resolution",
                        Value = "4K"
                    }
                ],
                Attributes =
                [
                    new ProductAttributeDetailDto
                    {
                        AttributeKey = "resolution",
                        Value = "4K",
                        ValueType = "string",
                        Confidence = 0.95m,
                        Evidence =
                        [
                            new AttributeEvidenceDto
                            {
                                SourceName = "Contoso",
                                SourceUrl = "https://example.test/products/1",
                                SourceProductId = "src-1",
                                SourceAttributeKey = "specs.resolution",
                                RawValue = "4K",
                                SelectorOrPath = "specs.resolution",
                                Confidence = 0.9m,
                                ObservedUtc = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
                            }
                        ]
                    }
                ],
                SourceProducts =
                [
                    new SourceProductDetailDto
                    {
                        Id = "src-1",
                        SourceName = "Contoso",
                        SourceUrl = "https://example.test/products/1",
                        Title = "Northwind OLED 55",
                        ModelNumber = "NW-55-OLED",
                        RawSchemaJson = "{}",
                        RawAttributes =
                        [
                            new SourceAttributeValueDto
                            {
                                AttributeKey = "resolution",
                                Value = "4K",
                                ValueType = "string",
                                SourcePath = "specs.resolution"
                            }
                        ]
                    }
                ]
            },
            ProductHistory =
            [
                new ProductChangeEventDto
                {
                    CanonicalProductId = "prod_tv_oled_001",
                    CategoryKey = "tv",
                    AttributeKey = "resolution",
                    OldValue = "1080p",
                    NewValue = "4K",
                    SourceName = "Contoso",
                    TimestampUtc = new DateTime(2025, 1, 16, 12, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Products/Details?productId=prod_tv_oled_001&category=tv&search=OLED&returnPage=3&minSourceCount=2&freshness=aging&conflictStatus=with_conflicts&completeness=partial&sort=conflicts_desc");

        Assert.That(html, Does.Contain("Jump to section"));
        Assert.That(html, Does.Contain("Back to explorer"));
        Assert.That(html, Does.Contain("Source comparison"));
        Assert.That(html, Does.Contain("Evidence Inspector"));
        Assert.That(html, Does.Contain("Conflict panel"));
        Assert.That(html, Does.Contain("Analyst workflow"));
        Assert.That(html, Does.Contain("Source drift indicators"));
        Assert.That(html, Does.Contain("Canonical value change explanations"));
        Assert.That(html, Does.Contain("Product history timeline"));
        Assert.That(html, Does.Contain("Attribute-level change history"));
        Assert.That(html, Does.Contain("Strongest current support comes from Contoso"));
        Assert.That(html, Does.Contain("Still aligns"));
        Assert.That(html, Does.Contain("Open source page"));
        Assert.That(html, Does.Contain("/Products?category=tv&amp;search=OLED&amp;page=3&amp;minSourceCount=2&amp;freshness=aging&amp;conflictStatus=with_conflicts&amp;completeness=partial&amp;sort=conflicts_desc"));
    }

    [Test]
    public async Task DetailPage_RendersNoHistoryStates()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Product = new ProductDetailDto
            {
                Id = "prod_tv_lcd_002",
                CategoryKey = "tv",
                Brand = "Northwind",
                ModelNumber = "NW-42-LCD",
                DisplayName = "Northwind LCD 42",
                CreatedUtc = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2025, 1, 18, 9, 15, 0, DateTimeKind.Utc),
                SourceCount = 1,
                EvidenceCount = 1,
                ConflictAttributeCount = 0,
                HasConflict = false,
                CompletenessScore = 0.8m,
                CompletenessStatus = "complete",
                FreshnessStatus = "fresh",
                FreshnessAgeDays = 1,
                Attributes =
                [
                    new ProductAttributeDetailDto
                    {
                        AttributeKey = "resolution",
                        Value = "4K",
                        ValueType = "string",
                        Confidence = 0.98m,
                        HasConflict = false,
                        Evidence =
                        [
                            new AttributeEvidenceDto
                            {
                                SourceName = "Contoso",
                                SourceUrl = "https://example.test/products/2",
                                SourceProductId = "src-2",
                                SourceAttributeKey = "specs.resolution",
                                RawValue = "4K",
                                SelectorOrPath = "specs.resolution",
                                Confidence = 0.98m,
                                ObservedUtc = new DateTime(2025, 1, 18, 9, 0, 0, DateTimeKind.Utc)
                            }
                        ]
                    }
                ],
                SourceProducts =
                [
                    new SourceProductDetailDto
                    {
                        Id = "src-2",
                        SourceName = "Contoso",
                        SourceUrl = "https://example.test/products/2",
                        Title = "Northwind LCD 42",
                        RawSchemaJson = "{}",
                        RawAttributes =
                        [
                            new SourceAttributeValueDto
                            {
                                AttributeKey = "resolution",
                                Value = "4K",
                                ValueType = "string",
                                SourcePath = "specs.resolution"
                            }
                        ]
                    }
                ]
            },
            ProductHistory = []
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Products/Details?productId=prod_tv_lcd_002&category=tv");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Product history timeline"));
            Assert.That(html, Does.Contain("No history events are recorded for this product yet."));
            Assert.That(html, Does.Contain("No attribute-level changes are recorded for this product yet."));
        });
    }
}
