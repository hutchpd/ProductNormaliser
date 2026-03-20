namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class CrawlPipelineOptions
{
    public const string SectionName = "Crawl";

    public string UserAgent { get; set; } = "ProductNormaliserBot/1.0";
    public int DefaultHostDelayMilliseconds { get; set; } = 1000;
    public Dictionary<string, int> HostDelayMilliseconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int TransientRetryCount { get; set; } = 2;
    public int WorkerIdleDelayMilliseconds { get; set; } = 1500;
}