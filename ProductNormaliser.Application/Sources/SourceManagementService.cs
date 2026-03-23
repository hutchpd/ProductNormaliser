using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceManagementService(
    ICrawlSourceStore crawlSourceStore,
    ICategoryMetadataService categoryMetadataService,
    ICrawlGovernanceService crawlGovernanceService,
    IManagementAuditService managementAuditService) : ISourceManagementService
{
    public async Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
    {
        return (await crawlSourceStore.ListAsync(cancellationToken))
            .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        ValidateSourceId(sourceId);
        return crawlSourceStore.GetAsync(NormaliseKey(sourceId), cancellationToken);
    }

    public async Task<CrawlSource> RegisterAsync(CrawlSourceRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var sourceId = NormaliseKey(registration.SourceId);
        if (await crawlSourceStore.GetAsync(sourceId, cancellationToken) is not null)
        {
            throw new ArgumentException($"Source '{sourceId}' already exists.", nameof(registration));
        }

        var now = DateTime.UtcNow;
        var baseUrl = ValidateBaseUrl(registration.BaseUrl, out var host, crawlGovernanceService);
        var supportedCategoryKeys = await ValidateCategoryKeysAsync(registration.SupportedCategoryKeys, cancellationToken);
        var source = new CrawlSource
        {
            Id = sourceId,
            DisplayName = ValidateDisplayName(registration.DisplayName),
            BaseUrl = baseUrl,
            Host = host,
            Description = NormaliseOptionalText(registration.Description),
            IsEnabled = registration.IsEnabled,
            SupportedCategoryKeys = supportedCategoryKeys,
            DiscoveryProfile = await NormaliseDiscoveryProfileAsync(registration.DiscoveryProfile, supportedCategoryKeys, baseUrl, host, cancellationToken),
            ThrottlingPolicy = NormaliseThrottlingPolicy(registration.ThrottlingPolicy),
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await crawlSourceStore.UpsertAsync(source, cancellationToken);
        return source;
    }

    public async Task<CrawlSource> UpdateAsync(string sourceId, CrawlSourceUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var existing = await RequireSourceAsync(sourceId, cancellationToken);
        existing.DisplayName = ValidateDisplayName(update.DisplayName);
        existing.BaseUrl = ValidateBaseUrl(update.BaseUrl, out var host, crawlGovernanceService);
        existing.Host = host;
        existing.Description = NormaliseOptionalText(update.Description);
        existing.DiscoveryProfile = update.DiscoveryProfile is null
            ? existing.DiscoveryProfile
            : await NormaliseDiscoveryProfileAsync(update.DiscoveryProfile, existing.SupportedCategoryKeys, existing.BaseUrl, host, cancellationToken);
        existing.UpdatedUtc = DateTime.UtcNow;

        await crawlSourceStore.UpsertAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<CrawlSource> EnableAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var existing = await RequireSourceAsync(sourceId, cancellationToken);
        var wasEnabled = existing.IsEnabled;
        existing.IsEnabled = true;
        existing.UpdatedUtc = DateTime.UtcNow;
        await crawlSourceStore.UpsertAsync(existing, cancellationToken);

        if (!wasEnabled)
        {
            await managementAuditService.RecordAsync(
                ManagementAuditActions.SourceEnabled,
                "source",
                existing.Id,
                new Dictionary<string, string>
                {
                    ["displayName"] = existing.DisplayName,
                    ["host"] = existing.Host
                },
                cancellationToken);
        }

        return existing;
    }

    public async Task<CrawlSource> DisableAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var existing = await RequireSourceAsync(sourceId, cancellationToken);
        var wasEnabled = existing.IsEnabled;
        existing.IsEnabled = false;
        existing.UpdatedUtc = DateTime.UtcNow;
        await crawlSourceStore.UpsertAsync(existing, cancellationToken);

        if (wasEnabled)
        {
            await managementAuditService.RecordAsync(
                ManagementAuditActions.SourceDisabled,
                "source",
                existing.Id,
                new Dictionary<string, string>
                {
                    ["displayName"] = existing.DisplayName,
                    ["host"] = existing.Host
                },
                cancellationToken);
        }

        return existing;
    }

    public async Task<CrawlSource> AssignCategoriesAsync(string sourceId, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
    {
        var existing = await RequireSourceAsync(sourceId, cancellationToken);
        var previousCategories = existing.SupportedCategoryKeys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        existing.SupportedCategoryKeys = await ValidateCategoryKeysAsync(categoryKeys, cancellationToken);
        existing.UpdatedUtc = DateTime.UtcNow;
        await crawlSourceStore.UpsertAsync(existing, cancellationToken);

        var updatedCategories = existing.SupportedCategoryKeys.ToArray();
        if (!previousCategories.SequenceEqual(updatedCategories, StringComparer.OrdinalIgnoreCase))
        {
            await managementAuditService.RecordAsync(
                ManagementAuditActions.SourceCategoriesChanged,
                "source",
                existing.Id,
                new Dictionary<string, string>
                {
                    ["previousCategories"] = string.Join(",", previousCategories),
                    ["updatedCategories"] = string.Join(",", updatedCategories)
                },
                cancellationToken);
        }

        return existing;
    }

    public async Task<CrawlSource> SetThrottlingAsync(string sourceId, SourceThrottlingPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var existing = await RequireSourceAsync(sourceId, cancellationToken);
        existing.ThrottlingPolicy = NormaliseThrottlingPolicy(policy);
        existing.UpdatedUtc = DateTime.UtcNow;
        await crawlSourceStore.UpsertAsync(existing, cancellationToken);
        return existing;
    }

    private async Task<CrawlSource> RequireSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        ValidateSourceId(sourceId);
        return await crawlSourceStore.GetAsync(NormaliseKey(sourceId), cancellationToken)
            ?? throw new KeyNotFoundException($"Source '{sourceId}' was not found.");
    }

    private async Task<List<string>> ValidateCategoryKeysAsync(IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken)
    {
        var keys = (categoryKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(NormaliseKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            return [];
        }

        var knownCategories = (await categoryMetadataService.ListAsync(enabledOnly: false, cancellationToken))
            .Select(category => category.CategoryKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownKeys = keys.Where(key => !knownCategories.Contains(key)).ToArray();
        if (unknownKeys.Length > 0)
        {
            throw new ArgumentException($"Unknown category keys: {string.Join(", ", unknownKeys)}.", nameof(categoryKeys));
        }

        return keys.ToList();
    }

    private static SourceThrottlingPolicy NormaliseThrottlingPolicy(SourceThrottlingPolicy? policy)
    {
        policy ??= new SourceThrottlingPolicy();

        if (policy.MinDelayMs < 0)
        {
            throw new ArgumentException("Minimum delay must be zero or greater.", nameof(policy));
        }

        if (policy.MaxDelayMs < policy.MinDelayMs)
        {
            throw new ArgumentException("Maximum delay must be greater than or equal to minimum delay.", nameof(policy));
        }

        if (policy.MaxConcurrentRequests <= 0)
        {
            throw new ArgumentException("Max concurrent requests must be greater than zero.", nameof(policy));
        }

        if (policy.RequestsPerMinute <= 0)
        {
            throw new ArgumentException("Requests per minute must be greater than zero.", nameof(policy));
        }

        return new SourceThrottlingPolicy
        {
            MinDelayMs = policy.MinDelayMs,
            MaxDelayMs = policy.MaxDelayMs,
            MaxConcurrentRequests = policy.MaxConcurrentRequests,
            RequestsPerMinute = policy.RequestsPerMinute,
            RespectRobotsTxt = policy.RespectRobotsTxt
        };
    }

    private async Task<SourceDiscoveryProfile> NormaliseDiscoveryProfileAsync(
        SourceDiscoveryProfile? profile,
        IReadOnlyCollection<string> supportedCategoryKeys,
        string baseUrl,
        string host,
        CancellationToken cancellationToken)
    {
        profile ??= new SourceDiscoveryProfile();

        if (profile.MaxDiscoveryDepth < 0)
        {
            throw new ArgumentException("Maximum discovery depth must be zero or greater.", nameof(profile));
        }

        if (profile.MaxUrlsPerRun <= 0)
        {
            throw new ArgumentException("Maximum URLs per run must be greater than zero.", nameof(profile));
        }

        var supportedCategorySet = supportedCategoryKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categoryEntryPages = await NormaliseCategoryEntryPagesAsync(profile.CategoryEntryPages, supportedCategorySet, baseUrl, host, cancellationToken);

        return new SourceDiscoveryProfile
        {
            CategoryEntryPages = categoryEntryPages,
            SitemapHints = NormaliseOrderedValues(profile.SitemapHints, value => NormaliseDiscoveryUrl(value, baseUrl, host)),
            AllowedPathPrefixes = NormaliseOrderedValues(profile.AllowedPathPrefixes, value => NormalisePathPrefix(value, baseUrl, host)),
            ExcludedPathPrefixes = NormaliseOrderedValues(profile.ExcludedPathPrefixes, value => NormalisePathPrefix(value, baseUrl, host)),
            ProductUrlPatterns = NormaliseOrderedValues(profile.ProductUrlPatterns, static value => value.Trim()),
            ListingUrlPatterns = NormaliseOrderedValues(profile.ListingUrlPatterns, static value => value.Trim()),
            MaxDiscoveryDepth = profile.MaxDiscoveryDepth,
            MaxUrlsPerRun = profile.MaxUrlsPerRun
        };
    }

    private async Task<Dictionary<string, List<string>>> NormaliseCategoryEntryPagesAsync(
        IReadOnlyDictionary<string, List<string>>? categoryEntryPages,
        IReadOnlySet<string> supportedCategoryKeys,
        string baseUrl,
        string host,
        CancellationToken cancellationToken)
    {
        if (categoryEntryPages is null || categoryEntryPages.Count == 0)
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var entryCategoryKeys = await ValidateCategoryKeysAsync(categoryEntryPages.Keys.ToArray(), cancellationToken);
        var unsupportedCategoryKeys = entryCategoryKeys
            .Where(categoryKey => !supportedCategoryKeys.Contains(categoryKey))
            .ToArray();
        if (unsupportedCategoryKeys.Length > 0)
        {
            throw new ArgumentException($"Discovery entry pages must target supported categories: {string.Join(", ", unsupportedCategoryKeys)}.", nameof(categoryEntryPages));
        }

        var normalised = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var categoryKey in entryCategoryKeys)
        {
            categoryEntryPages.TryGetValue(categoryKey, out var urls);
            normalised[categoryKey] = NormaliseOrderedValues(urls ?? [], value => NormaliseDiscoveryUrl(value, baseUrl, host));
        }

        return normalised;
    }

    private string NormaliseDiscoveryUrl(string value, string baseUrl, string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Uri.TryCreate(value.Trim(), UriKind.RelativeOrAbsolute, out var uri))
        {
            throw new ArgumentException($"Discovery URL '{value}' is not a valid relative or absolute URL.", nameof(value));
        }

        var baseUri = new Uri(AppendTrailingSlash(baseUrl), UriKind.Absolute);
        var absoluteUri = uri.IsAbsoluteUri ? uri : new Uri(baseUri, uri);

        crawlGovernanceService.ValidateSourceBaseUrl(absoluteUri.ToString(), nameof(value));
        if (!string.Equals(absoluteUri.Host, host, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Discovery URL '{value}' must remain on source host '{host}'.", nameof(value));
        }

        return absoluteUri.ToString().TrimEnd('/');
    }

    private string NormalisePathPrefix(string value, string baseUrl, string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var absoluteUri))
        {
            crawlGovernanceService.ValidateSourceBaseUrl(absoluteUri.ToString(), nameof(value));
            if (!string.Equals(absoluteUri.Host, host, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Path prefix '{value}' must remain on source host '{host}'.", nameof(value));
            }

            return NormalisePathSegment(absoluteUri.PathAndQuery);
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Relative, out var relativeUri))
        {
            return NormalisePathSegment(relativeUri.OriginalString);
        }

        var combinedUri = new Uri(new Uri(AppendTrailingSlash(baseUrl), UriKind.Absolute), value.Trim());
        if (!string.Equals(combinedUri.Host, host, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path prefix '{value}' must remain on source host '{host}'.", nameof(value));
        }

        return NormalisePathSegment(combinedUri.PathAndQuery);
    }

    private static List<string> NormaliseOrderedValues(IEnumerable<string> values, Func<string, string> normalise)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalisedValue = normalise(value);
            if (seen.Add(normalisedValue))
            {
                result.Add(normalisedValue);
            }
        }

        return result;
    }

    private static string NormalisePathSegment(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return "/";
        }

        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = $"/{trimmed}";
        }

        return trimmed.Length > 1 ? trimmed.TrimEnd('/') : trimmed;
    }

    private static string AppendTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }

    private static string ValidateDisplayName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static string ValidateBaseUrl(string value, out string host, ICrawlGovernanceService crawlGovernanceService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Base URL must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        crawlGovernanceService.ValidateSourceBaseUrl(value, nameof(value));

        host = uri.Host.ToLowerInvariant();
        return uri.ToString().TrimEnd('/');
    }

    private static string? NormaliseOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateSourceId(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
    }

    private static string NormaliseKey(string value)
    {
        return value
            .Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}