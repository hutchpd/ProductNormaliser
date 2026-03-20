using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Options;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class HttpFetcher(HttpClient httpClient, IOptions<CrawlPipelineOptions> options) : IHttpFetcher
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> HostLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastRequestByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly CrawlPipelineOptions crawlOptions = options.Value;

    public async Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var uri = new Uri(target.Url, UriKind.Absolute);
        var hostKey = uri.Host;
        var hostLock = HostLocks.GetOrAdd(hostKey, _ => new SemaphoreSlim(1, 1));

        await hostLock.WaitAsync(cancellationToken);
        try
        {
            await RespectRateLimitAsync(hostKey, cancellationToken);

            for (var attempt = 0; attempt <= crawlOptions.TransientRetryCount; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.TryAddWithoutValidation("User-Agent", crawlOptions.UserAgent);

                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    var html = response.Content is null
                        ? null
                        : await response.Content.ReadAsStringAsync(cancellationToken);

                    if (IsTransientStatusCode(response.StatusCode) && attempt < crawlOptions.TransientRetryCount)
                    {
                        await Task.Delay(GetBackoff(attempt), cancellationToken);
                        continue;
                    }

                    LastRequestByHost[hostKey] = DateTimeOffset.UtcNow;
                    return new FetchResult
                    {
                        Url = target.Url,
                        IsSuccess = response.IsSuccessStatusCode,
                        StatusCode = (int)response.StatusCode,
                        Html = html,
                        FailureReason = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                        FetchedUtc = DateTime.UtcNow
                    };
                }
                catch (HttpRequestException exception) when (attempt < crawlOptions.TransientRetryCount)
                {
                    await Task.Delay(GetBackoff(attempt), cancellationToken);
                    if (attempt == crawlOptions.TransientRetryCount - 1)
                    {
                        return Failed(target.Url, exception.Message);
                    }
                }
            }

            return Failed(target.Url, "Transient HTTP failure after retries.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failed(target.Url, exception.Message);
        }
        finally
        {
            hostLock.Release();
        }
    }

    private async Task RespectRateLimitAsync(string hostKey, CancellationToken cancellationToken)
    {
        var delay = GetHostDelay(hostKey);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        if (LastRequestByHost.TryGetValue(hostKey, out var lastRequestUtc))
        {
            var remainingDelay = delay - (DateTimeOffset.UtcNow - lastRequestUtc);
            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay, cancellationToken);
            }
        }
    }

    private TimeSpan GetHostDelay(string hostKey)
    {
        return crawlOptions.HostDelayMilliseconds.TryGetValue(hostKey, out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : TimeSpan.FromMilliseconds(crawlOptions.DefaultHostDelayMilliseconds);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return numericStatusCode == 408 || numericStatusCode == 429 || numericStatusCode >= 500;
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        return TimeSpan.FromMilliseconds(250 * (attempt + 1));
    }

    private static FetchResult Failed(string url, string reason)
    {
        return new FetchResult
        {
            Url = url,
            IsSuccess = false,
            StatusCode = 0,
            FailureReason = reason,
            FetchedUtc = DateTime.UtcNow
        };
    }
}