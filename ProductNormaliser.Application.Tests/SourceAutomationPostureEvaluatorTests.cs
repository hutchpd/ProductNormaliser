using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Intelligence)]
public sealed class SourceAutomationPostureEvaluatorTests
{
    [Test]
    public void Evaluate_KeepsAutoAccept_WhenLongitudinalSignalsRemainHealthy()
    {
        var evaluator = new SourceAutomationPostureEvaluator(new SourceAutomationMonitoringOptions());

        var posture = evaluator.Evaluate(
            SourceAutomationModes.AutoAcceptAndSeed,
            [
                CreateSnapshot("tv", new DateTime(2026, 03, 25, 10, 00, 00, DateTimeKind.Utc), 0.88m, 0.78m, 0.66m, 0.82m, 0.14m, 0.71m),
                CreateSnapshot("tv", new DateTime(2026, 03, 18, 10, 00, 00, DateTimeKind.Utc), 0.84m, 0.75m, 0.61m, 0.79m, 0.18m, 0.68m),
                CreateSnapshot("monitor", new DateTime(2026, 03, 24, 10, 00, 00, DateTimeKind.Utc), 0.85m, 0.73m, 0.58m, 0.77m, 0.16m, 0.64m),
                CreateSnapshot("monitor", new DateTime(2026, 03, 17, 10, 00, 00, DateTimeKind.Utc), 0.82m, 0.70m, 0.56m, 0.74m, 0.20m, 0.61m)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(posture.Status, Is.EqualTo(SourceAutomationPosture.StatusHealthy));
            Assert.That(posture.EffectiveMode, Is.EqualTo(SourceAutomationModes.AutoAcceptAndSeed));
            Assert.That(posture.RecommendedAction, Is.EqualTo(SourceAutomationPosture.ActionKeepCurrentMode));
            Assert.That(posture.BlockingReasons, Is.Empty);
            Assert.That(posture.SupportingReasons, Is.Not.Empty);
        });
    }

    [Test]
    public void Evaluate_DowngradesAndFlagsManualReview_WhenSignalsDegrade()
    {
        var evaluator = new SourceAutomationPostureEvaluator(new SourceAutomationMonitoringOptions());

        var posture = evaluator.Evaluate(
            SourceAutomationModes.AutoAcceptAndSeed,
            [
                CreateSnapshot("tv", new DateTime(2026, 03, 25, 10, 00, 00, DateTimeKind.Utc), 0.58m, 0.38m, 0.29m, 0.31m, 0.52m, 0.28m, priceVolatilityScore: 0.88m),
                CreateSnapshot("tv", new DateTime(2026, 03, 18, 10, 00, 00, DateTimeKind.Utc), 0.79m, 0.67m, 0.51m, 0.69m, 0.22m, 0.59m),
                CreateSnapshot("monitor", new DateTime(2026, 03, 24, 10, 00, 00, DateTimeKind.Utc), 0.52m, 0.34m, 0.25m, 0.28m, 0.61m, 0.22m, priceVolatilityScore: 0.91m),
                CreateSnapshot("monitor", new DateTime(2026, 03, 17, 10, 00, 00, DateTimeKind.Utc), 0.76m, 0.62m, 0.49m, 0.66m, 0.24m, 0.55m)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(posture.Status, Is.EqualTo(SourceAutomationPosture.StatusManualReview));
            Assert.That(posture.EffectiveMode, Is.EqualTo(SourceAutomationModes.OperatorAssisted));
            Assert.That(posture.RecommendedAction, Is.EqualTo(SourceAutomationPosture.ActionFlagManualReview));
            Assert.That(posture.BlockingReasons, Is.Not.Empty);
            Assert.That(posture.BlockingReasons.Any(reason => reason.Contains("Discovery breadth", StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    [Test]
    public void Evaluate_PromotesSuggestMode_WhenRecurringDiscoveryKeepsFindingAcceptedCandidates()
    {
        var evaluator = new SourceAutomationPostureEvaluator(new SourceAutomationMonitoringOptions());

        var posture = evaluator.Evaluate(
            SourceAutomationModes.SuggestAccept,
            [],
            new SourceRecurringDiscoveryHistory
            {
                DiscoveryRunCount = 3,
                AcceptedCandidateCount = 2,
                SuggestedCandidateCount = 1
            });

        Assert.Multiple(() =>
        {
            Assert.That(posture.EffectiveMode, Is.EqualTo(SourceAutomationModes.SuggestAccept));
            Assert.That(posture.RecurringDiscoveryRunCount, Is.EqualTo(3));
            Assert.That(posture.RecurringDiscoveryAcceptedCount, Is.EqualTo(2));
            Assert.That(posture.SupportingReasons.Any(reason => reason.Contains("Recurring discovery", StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    [Test]
    public void Evaluate_FlagsManualReview_WhenRecurringDiscoveryMostlyEndsInDismissals()
    {
        var evaluator = new SourceAutomationPostureEvaluator(new SourceAutomationMonitoringOptions());

        var posture = evaluator.Evaluate(
            SourceAutomationModes.AutoAcceptAndSeed,
            [],
            new SourceRecurringDiscoveryHistory
            {
                DiscoveryRunCount = 4,
                AcceptedCandidateCount = 0,
                DismissedCandidateCount = 3,
                SupersededCandidateCount = 1
            });

        Assert.Multiple(() =>
        {
            Assert.That(posture.Status, Is.EqualTo(SourceAutomationPosture.StatusManualReview));
            Assert.That(posture.RecommendedAction, Is.EqualTo(SourceAutomationPosture.ActionFlagManualReview));
            Assert.That(posture.BlockingReasons.Any(reason => reason.Contains("dismiss or supersede", StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    private static SourceQualitySnapshot CreateSnapshot(
        string categoryKey,
        DateTime timestampUtc,
        decimal trustScore,
        decimal discoveryBreadthScore,
        decimal productTargetPromotionRate,
        decimal extractabilityRate,
        decimal noProductRate,
        decimal downstreamYieldScore,
        decimal specStabilityScore = 0.70m,
        decimal priceVolatilityScore = 0.30m)
    {
        return new SourceQualitySnapshot
        {
            Id = $"snapshot:{categoryKey}:{timestampUtc:yyyyMMddHHmmss}",
            SourceName = "alpha",
            CategoryKey = categoryKey,
            TimestampUtc = timestampUtc,
            HistoricalTrustScore = trustScore,
            DiscoveryBreadthScore = discoveryBreadthScore,
            ProductTargetPromotionRate = productTargetPromotionRate,
            ExtractabilityRate = extractabilityRate,
            NoProductRate = noProductRate,
            DownstreamYieldScore = downstreamYieldScore,
            SpecStabilityScore = specStabilityScore,
            PriceVolatilityScore = priceVolatilityScore
        };
    }
}