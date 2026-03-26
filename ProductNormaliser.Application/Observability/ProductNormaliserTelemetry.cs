using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ProductNormaliser.Application.Observability;

public static class ProductNormaliserTelemetry
{
    public const string TelemetryName = "ProductNormaliser.Operations";

    public static readonly ActivitySource ActivitySource = new(TelemetryName);

    private static readonly Meter Meter = new(TelemetryName, "1.0.0");

    public static readonly Counter<long> CrawlJobsCreated = Meter.CreateCounter<long>(
        "productnormaliser.crawl.jobs.created",
        unit: "{job}",
        description: "Number of crawl jobs created by operators.");

    public static readonly Counter<long> CrawlJobsStarted = Meter.CreateCounter<long>(
        "productnormaliser.crawl.jobs.started",
        unit: "{job}",
        description: "Number of crawl jobs transitioned to running.");

    public static readonly Counter<long> CrawlJobsCompleted = Meter.CreateCounter<long>(
        "productnormaliser.crawl.jobs.completed",
        unit: "{job}",
        description: "Number of crawl jobs reaching a terminal state.");

    public static readonly Counter<long> CrawlJobTargetsRecorded = Meter.CreateCounter<long>(
        "productnormaliser.crawl.job.targets.recorded",
        unit: "{target}",
        description: "Recorded crawl job target outcomes by category and status.");

    public static readonly Counter<long> CrawlQueueDequeued = Meter.CreateCounter<long>(
        "productnormaliser.crawl.queue.dequeued",
        unit: "{lease}",
        description: "Number of queue items leased by the worker.");

    public static readonly Counter<long> CrawlQueueRetried = Meter.CreateCounter<long>(
        "productnormaliser.crawl.queue.retried",
        unit: "{item}",
        description: "Number of queue items rescheduled for retry.");

    public static readonly Counter<long> CrawlQueueTerminalOutcomes = Meter.CreateCounter<long>(
        "productnormaliser.crawl.queue.outcomes",
        unit: "{item}",
        description: "Queue item terminal outcomes by status.");

    public static readonly Counter<long> CrawlTargetsProcessed = Meter.CreateCounter<long>(
        "productnormaliser.crawl.targets.processed",
        unit: "{target}",
        description: "Worker crawl target processing outcomes by source, category, and status.");

    public static readonly Counter<long> CrawlProductsExtracted = Meter.CreateCounter<long>(
        "productnormaliser.crawl.products.extracted",
        unit: "{product}",
        description: "Structured products extracted from crawled pages.");

    public static readonly Histogram<double> CrawlTargetDurationMs = Meter.CreateHistogram<double>(
        "productnormaliser.crawl.target.duration",
        unit: "ms",
        description: "End-to-end crawl processing duration per target.");

    public static readonly Histogram<int> CrawlJobTargetCount = Meter.CreateHistogram<int>(
        "productnormaliser.crawl.job.target_count",
        unit: "{target}",
        description: "Number of targets included in each crawl job.");

    public static readonly Counter<long> DiscoveryRunsStarted = Meter.CreateCounter<long>(
        "productnormaliser.discovery.runs.started",
        unit: "{run}",
        description: "Number of persisted discovery runs started by the worker.");

    public static readonly Counter<long> DiscoveryRunsRecovered = Meter.CreateCounter<long>(
        "productnormaliser.discovery.runs.recovered",
        unit: "{run}",
        description: "Number of stale discovery runs re-queued by the recovery sweeper.");

    public static readonly Counter<long> DiscoveryRunsFailedRecovery = Meter.CreateCounter<long>(
        "productnormaliser.discovery.runs.recovery_failed",
        unit: "{run}",
        description: "Number of stale discovery runs failed after exhausting recovery attempts.");

    public static readonly Counter<long> DiscoveryCandidatesProcessed = Meter.CreateCounter<long>(
        "productnormaliser.discovery.candidates.processed",
        unit: "{candidate}",
        description: "Candidate processing outcomes by final state.");

    public static readonly Counter<long> DiscoveryCandidatesArchived = Meter.CreateCounter<long>(
        "productnormaliser.discovery.candidates.archived",
        unit: "{candidate}",
        description: "Candidates moved into archive by suppression or retention policy.");

    public static readonly Histogram<double> DiscoverySearchDurationMs = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.search.duration",
        unit: "ms",
        description: "Search-provider latency per discovery run.");

    public static readonly Histogram<double> DiscoveryProbeDurationMs = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.probe.duration",
        unit: "ms",
        description: "Probe latency per candidate.");

    public static readonly Histogram<double> DiscoveryLlmDurationMs = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.llm.duration",
        unit: "ms",
        description: "LLM verification latency per candidate.");

    public static readonly Histogram<int> DiscoveryLlmQueueDepth = Meter.CreateHistogram<int>(
        "productnormaliser.discovery.llm.queue_depth",
        unit: "{candidate}",
        description: "Observed LLM queue depth while a discovery run is processing candidates.");

    public static readonly Histogram<double> DiscoveryCandidateThroughputPerMinute = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.candidate_throughput_per_minute",
        unit: "{candidate}/min",
        description: "Observed candidate throughput per run.");

    public static readonly Histogram<double> DiscoveryAcceptanceRate = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.acceptance_rate",
        unit: "ratio",
        description: "Accepted-candidate ratio per discovery run.");

    public static readonly Histogram<double> DiscoveryManualReviewRate = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.manual_review_rate",
        unit: "ratio",
        description: "Manual-review ratio per discovery run.");

    public static readonly Histogram<double> DiscoveryTimeToFirstAcceptedMs = Meter.CreateHistogram<double>(
        "productnormaliser.discovery.time_to_first_accepted",
        unit: "ms",
        description: "Elapsed time from run start to first accepted candidate.");
}
