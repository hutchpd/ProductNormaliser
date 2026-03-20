namespace ProductNormaliser.Core.Models;

public sealed class FetchResult
{
    public string Url { get; set; } = default!;
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? Html { get; set; }
    public string? FailureReason { get; set; }
    public DateTime FetchedUtc { get; set; }
}