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
}
