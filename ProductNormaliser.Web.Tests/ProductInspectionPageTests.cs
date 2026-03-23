using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;

namespace ProductNormaliser.Web.Tests;

public sealed class ProductInspectionPageTests
{
    [Test]
    public async Task ProductDetails_OnGetAsync_LoadsProductAndBuildsTimeline()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TV", IsEnabled = true }],
            ProductPage = new ProductListResponseDto
            {
                Items = [new ProductSummaryDto { Id = "canon-1", CategoryKey = "tv", Brand = "Sony", DisplayName = "Sony Bravia" }],
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            },
            Product = CreateProduct(),
            ProductHistory =
            [
                new ProductChangeEventDto
                {
                    CanonicalProductId = "canon-1",
                    CategoryKey = "tv",
                    AttributeKey = "screen_size",
                    OldValue = 54.6,
                    NewValue = 55,
                    SourceName = "AO UK",
                    TimestampUtc = new DateTime(2026, 03, 22, 10, 05, 00, DateTimeKind.Utc)
                }
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Products.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.DetailsModel>.Instance)
        {
            ProductId = "canon-1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Product, Is.Not.Null);
            Assert.That(model.SourceComparisonRows, Has.Count.EqualTo(2));
            Assert.That(model.ConflictRows.Select(row => row.AttributeKey), Is.EqualTo(new[] { "screen_size" }));
            Assert.That(model.Timeline, Has.Count.EqualTo(1));
            Assert.That(model.Timeline[0].ChangeSummary, Is.EqualTo("Changed from 54.6 to 55"));
            Assert.That(model.Timeline[0].SourceAlignmentBadge.Text, Is.EqualTo("Still aligns"));
            Assert.That(model.AttributeHistory, Has.Count.EqualTo(1));
            Assert.That(model.SourceDriftIndicators, Has.Count.EqualTo(2));
            Assert.That(model.CanonicalExplanations, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void ProductInspectionPresentation_BuildsAttributeHistorySourceDriftAndCanonicalExplanationState()
    {
        var history = new[]
        {
            new ProductChangeEventDto
            {
                CanonicalProductId = "canon-1",
                CategoryKey = "tv",
                AttributeKey = "screen_size",
                OldValue = 54.6,
                NewValue = 55,
                SourceName = "AO UK",
                TimestampUtc = new DateTime(2026, 03, 22, 10, 05, 00, DateTimeKind.Utc)
            },
            new ProductChangeEventDto
            {
                CanonicalProductId = "canon-1",
                CategoryKey = "tv",
                AttributeKey = "screen_size",
                OldValue = 55,
                NewValue = 54.6,
                SourceName = "Currys",
                TimestampUtc = new DateTime(2026, 03, 22, 09, 58, 00, DateTimeKind.Utc)
            }
        };

        var attributeHistory = ProductInspectionPresentation.GetAttributeHistory(CreateProduct(), history);
        var sourceDrift = ProductInspectionPresentation.GetSourceDriftIndicators(CreateProduct(), history);
        var explanations = ProductInspectionPresentation.GetCanonicalExplanations(CreateProduct(), history);

        var screenSizeHistory = attributeHistory.Single(group => group.AttributeKey == "screen_size");
        var currysDrift = sourceDrift.Single(indicator => indicator.SourceName == "Currys");
        var screenSizeExplanation = explanations.Single(explanation => explanation.AttributeKey == "screen_size");

        Assert.Multiple(() =>
        {
            Assert.That(screenSizeHistory.HasConflictingHistory, Is.True);
            Assert.That(screenSizeHistory.HistoryBadge.Text, Is.EqualTo("Conflicting history"));
            Assert.That(screenSizeHistory.Events, Has.Count.EqualTo(2));

            Assert.That(currysDrift.DriftBadge.Text, Is.EqualTo("Watch"));
            Assert.That(currysDrift.DivergingAttributeCount, Is.EqualTo(1));
            Assert.That(currysDrift.DivergingAttributes, Is.EqualTo(new[] { "screen_size" }));

            Assert.That(screenSizeExplanation.DecisionBadge.Text, Is.EqualTo("Contested"));
            Assert.That(screenSizeExplanation.SupportingSources, Is.EqualTo(new[] { "AO UK" }));
            Assert.That(screenSizeExplanation.OpposingSources, Is.EqualTo(new[] { "Currys" }));
            Assert.That(screenSizeExplanation.StrongestSupportSummary, Does.Contain("AO UK"));
            Assert.That(screenSizeExplanation.LastChangedSummary, Does.Contain("AO UK"));
        });
    }

    [Test]
    public void ProductInspectionPresentation_BuildsConflictRenderingState()
    {
        var conflicts = ProductInspectionPresentation.GetConflictRows(CreateProduct());

        Assert.Multiple(() =>
        {
            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].AttributeKey, Is.EqualTo("screen_size"));
            Assert.That(conflicts[0].CanonicalValue, Is.EqualTo("55 in"));
            Assert.That(conflicts[0].Groups.Single(group => group.IsCanonical).Sources, Is.EqualTo(new[] { "AO UK" }));
            Assert.That(conflicts[0].Groups.Single(group => !group.IsCanonical).DisplayValue, Is.EqualTo("54.6 in"));
        });
    }

    [Test]
    public void ProductInspectionPresentation_BuildsEvidenceRenderingState()
    {
        var evidenceRows = ProductInspectionPresentation.GetEvidenceInspectorRows(CreateProduct());
        var screenSize = evidenceRows.Single(row => row.AttributeKey == "screen_size");

        Assert.Multiple(() =>
        {
            Assert.That(screenSize.HasConflict, Is.True);
            Assert.That(screenSize.Evidence, Has.Count.EqualTo(2));
            Assert.That(screenSize.Evidence.Single(item => item.SourceName == "AO UK").MatchesCanonical, Is.True);
            Assert.That(screenSize.Evidence.Single(item => item.SourceName == "Currys").MatchesCanonical, Is.False);
            Assert.That(screenSize.Evidence.Single(item => item.SourceName == "Currys").SelectorOrPath, Is.EqualTo("specifications.screenSize"));
        });
    }

    [Test]
    public void ProductInspectionPresentation_BuildsSourceComparisonLayoutStates()
    {
        var rows = ProductInspectionPresentation.GetSourceComparisonRows(CreateProduct());
        var screenSize = rows.Single(row => row.AttributeKey == "screen_size");
        var panelType = rows.Single(row => row.AttributeKey == "panel_type");

        Assert.Multiple(() =>
        {
            Assert.That(screenSize.HasConflict, Is.True);
            Assert.That(screenSize.Cells.Single(cell => cell.SourceName == "AO UK").MatchesCanonical, Is.True);
            Assert.That(screenSize.Cells.Single(cell => cell.SourceName == "Currys").HasDisagreement, Is.True);
            Assert.That(panelType.Cells.Single(cell => cell.SourceName == "Currys").HasClaim, Is.False);
        });
    }

    private static ProductDetailDto CreateProduct()
    {
        return new ProductDetailDto
        {
            Id = "canon-1",
            CategoryKey = "tv",
            Brand = "Sony",
            ModelNumber = "XR-55A80L",
            Gtin = "1234567890123",
            DisplayName = "Sony Bravia XR 55",
            CreatedUtc = new DateTime(2026, 03, 21, 09, 00, 00, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 03, 22, 10, 10, 00, DateTimeKind.Utc),
            SourceCount = 2,
            EvidenceCount = 3,
            ConflictAttributeCount = 1,
            HasConflict = true,
            CompletenessScore = 0.75m,
            CompletenessStatus = "partial",
            PopulatedKeyAttributeCount = 3,
            ExpectedKeyAttributeCount = 4,
            FreshnessStatus = "fresh",
            FreshnessAgeDays = 1,
            KeyAttributes = [new ProductKeyAttributeDto { AttributeKey = "panel_type", DisplayName = "Panel Type", Value = "OLED" }],
            Attributes =
            [
                new ProductAttributeDetailDto
                {
                    AttributeKey = "panel_type",
                    Value = "OLED",
                    ValueType = "string",
                    Confidence = 0.97m,
                    HasConflict = false,
                    Evidence =
                    [
                        new AttributeEvidenceDto
                        {
                            SourceName = "AO UK",
                            SourceUrl = "https://ao.example/p/ao-1",
                            SourceProductId = "ao-1",
                            SourceAttributeKey = "panel_type",
                            RawValue = "OLED",
                            SelectorOrPath = "specs.panelType",
                            Confidence = 0.95m,
                            ObservedUtc = new DateTime(2026, 03, 22, 10, 00, 00, DateTimeKind.Utc)
                        }
                    ]
                },
                new ProductAttributeDetailDto
                {
                    AttributeKey = "screen_size",
                    Value = 55,
                    ValueType = "number",
                    Unit = "in",
                    Confidence = 0.93m,
                    HasConflict = true,
                    Evidence =
                    [
                        new AttributeEvidenceDto
                        {
                            SourceName = "AO UK",
                            SourceUrl = "https://ao.example/p/ao-1",
                            SourceProductId = "ao-1",
                            SourceAttributeKey = "screen_size",
                            RawValue = "55",
                            SelectorOrPath = "specs.screenSize",
                            Confidence = 0.94m,
                            ObservedUtc = new DateTime(2026, 03, 22, 10, 01, 00, DateTimeKind.Utc)
                        },
                        new AttributeEvidenceDto
                        {
                            SourceName = "Currys",
                            SourceUrl = "https://currys.example/p/c-1",
                            SourceProductId = "currys-1",
                            SourceAttributeKey = "screen_size",
                            RawValue = "54.6",
                            SelectorOrPath = "specifications.screenSize",
                            Confidence = 0.89m,
                            ObservedUtc = new DateTime(2026, 03, 22, 10, 02, 00, DateTimeKind.Utc)
                        }
                    ]
                }
            ],
            SourceProducts =
            [
                new SourceProductDetailDto
                {
                    Id = "ao-1",
                    SourceName = "AO UK",
                    SourceUrl = "https://ao.example/p/ao-1",
                    ModelNumber = "XR-55A80L",
                    Gtin = "1234567890123",
                    Title = "Sony Bravia XR 55 OLED",
                    RawSchemaJson = "{}",
                    RawAttributes =
                    [
                        new SourceAttributeValueDto { AttributeKey = "panel_type", Value = "OLED", ValueType = "string", SourcePath = "specs.panelType" },
                        new SourceAttributeValueDto { AttributeKey = "screen_size", Value = "55", ValueType = "number", Unit = "in", SourcePath = "specs.screenSize" }
                    ]
                },
                new SourceProductDetailDto
                {
                    Id = "currys-1",
                    SourceName = "Currys",
                    SourceUrl = "https://currys.example/p/c-1",
                    ModelNumber = "XR55A80L",
                    Gtin = "1234567890123",
                    Title = "Sony 55 inch OLED TV",
                    RawSchemaJson = "{}",
                    RawAttributes =
                    [
                        new SourceAttributeValueDto { AttributeKey = "screen_size", Value = "54.6", ValueType = "number", Unit = "in", SourcePath = "specifications.screenSize" }
                    ]
                }
            ]
        };
    }
}