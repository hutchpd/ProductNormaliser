using System.Collections.Concurrent;
using System.Net;
using ProductNormaliser.Application.Sources;
using Microsoft.Extensions.Options;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class HttpFetcher(HttpClient httpClient, IOptions<CrawlPipelineOptions> options, ICrawlSourceStore crawlSourceStore) : IHttpFetcher
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> HostLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastRequestByThrottleKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly CrawlPipelineOptions crawlOptions = options.Value;

    public async Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var uri = new Uri(target.Url, UriKind.Absolute);
        var source = await ResolveSourceAsync(target, cancellationToken);
        if (source is not null && !source.GetDiscoveryHosts().Contains(NormaliseHost(uri.Host), StringComparer.OrdinalIgnoreCase))
        {
            return Failed(target.Url, $"Host '{uri.Host}' is not allowed for source '{source.Id}'.");
        }

        var throttleProfile = await ResolveThrottleProfileAsync(target, uri, cancellationToken);
        var hostLock = HostLocks.GetOrAdd(throttleProfile.ThrottleKey, _ => new SemaphoreSlim(throttleProfile.MaxConcurrentRequests, throttleProfile.MaxConcurrentRequests));

        await hostLock.WaitAsync(cancellationToken);
        try
        {
            await RespectRateLimitAsync(throttleProfile, cancellationToken);

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

                    LastRequestByThrottleKey[throttleProfile.ThrottleKey] = DateTimeOffset.UtcNow;
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

    private async Task RespectRateLimitAsync(FetchThrottleProfile throttleProfile, CancellationToken cancellationToken)
    {
        var delay = throttleProfile.Delay;
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        if (LastRequestByThrottleKey.TryGetValue(throttleProfile.ThrottleKey, out var lastRequestUtc))
        {
            var remainingDelay = delay - (DateTimeOffset.UtcNow - lastRequestUtc);
            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay, cancellationToken);
            }
        }
    }

    private async Task<FetchThrottleProfile> ResolveThrottleProfileAsync(CrawlTarget target, Uri uri, CancellationToken cancellationToken)
    {
        var source = await ResolveSourceAsync(target, cancellationToken);
        if (source is not null
            && source.GetDiscoveryHosts().Contains(NormaliseHost(uri.Host), StringComparer.OrdinalIgnoreCase))
        {
            return new FetchThrottleProfile(
                ThrottleKey: $"source:{source.Id}",
                MaxConcurrentRequests: Math.Max(1, source.ThrottlingPolicy.MaxConcurrentRequests),
                Delay: GetSourceDelay(source.ThrottlingPolicy));
        }

        return new FetchThrottleProfile(
            ThrottleKey: $"host:{uri.Host}",
            MaxConcurrentRequests: 1,
            Delay: GetDefaultHostDelay(uri.Host));
    }

    private async Task<CrawlSource?> ResolveSourceAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        if (!target.Metadata.TryGetValue("sourceName", out var sourceName) || string.IsNullOrWhiteSpace(sourceName))
        {
            return null;
        }

        var normalized = sourceName.Trim();
        var source = await crawlSourceStore.GetAsync(normalized, cancellationToken);
        if (source is not null)
        {
            return source;
        }

        var sources = await crawlSourceStore.ListAsync(cancellationToken);
        return sources.FirstOrDefault(item => string.Equals(item.DisplayName, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private TimeSpan GetDefaultHostDelay(string hostKey)
    {
        return crawlOptions.HostDelayMilliseconds.TryGetValue(hostKey, out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : TimeSpan.FromMilliseconds(crawlOptions.DefaultHostDelayMilliseconds);
    }

    private static TimeSpan GetSourceDelay(SourceThrottlingPolicy policy)
    {
        var minDelay = TimeSpan.FromMilliseconds(Math.Max(0, policy.MinDelayMs));
        var maxDelay = TimeSpan.FromMilliseconds(Math.Max(policy.MinDelayMs, policy.MaxDelayMs));
        var rpmDelay = policy.RequestsPerMinute <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromMinutes(1d / policy.RequestsPerMinute);

        var desiredDelay = minDelay > rpmDelay ? minDelay : rpmDelay;
        return desiredDelay > maxDelay ? maxDelay : desiredDelay;
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

    private static string NormaliseHost(string host)
    {
        var trimmed = host.Trim().TrimEnd('.');
        return trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..]
            : trimmed;
    }

    private sealed record FetchThrottleProfile(string ThrottleKey, int MaxConcurrentRequests, TimeSpan Delay);
}