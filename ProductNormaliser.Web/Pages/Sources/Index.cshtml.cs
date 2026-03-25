using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ProductNormaliser.Web.Pages.Sources;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "enabled")]
    public bool? Enabled { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public RegisterSourceInput Registration { get; set; } = new();

    [BindProperty]
    public DiscoverSourceCandidatesInput CandidateDiscovery { get; set; } = new();

    [BindProperty]
    public UseCandidateInput CandidateSelection { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public string? CandidateDiscoveryErrorMessage { get; private set; }

    public SourceCandidateDiscoveryResponseDto? CandidateDiscoveryResult { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SourceDto> AllSources { get; private set; } = [];

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public int TotalSources { get; private set; }

    public int ReadySources => Sources.Count(source => string.Equals(source.Readiness.Status, "Ready", StringComparison.OrdinalIgnoreCase));

    public int AttentionSources => Sources.Count(source => string.Equals(source.Health.Status, "Attention", StringComparison.OrdinalIgnoreCase));

    public int DiscoveryBacklogSources => Sources.Count(source => source.DiscoveryQueueDepth > 0);

    public int ActiveDiscoverySources => Sources.Count(source => source.LastDiscoveryUtc is not null || source.ConfirmedProductUrlsLast24Hours > 0);

    public int RegistryEnabledSources => AllSources.Count(source => source.IsEnabled);

    public int RegistryDiscoveryConfiguredSources => AllSources.Count(HasDiscoveryScaffold);

    public int RegistryBootReadySources => AllSources.Count(IsBootReady);

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Source Registry",
        Title = "Manage enabled hosts and source readiness",
        Description = "Review each crawl host’s assigned category coverage, throttling posture, readiness, health indicators, and recent crawl activity before changing source state.",
        Metrics =
        [
            new HeroMetricModel { Label = "Filtered", Value = Sources.Count.ToString() },
            new HeroMetricModel { Label = "Total", Value = TotalSources.ToString() },
            new HeroMetricModel { Label = "Enabled", Value = Sources.Count(source => source.IsEnabled).ToString() },
            new HeroMetricModel { Label = "Discovery active", Value = ActiveDiscoverySources.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        return await RegisterAsync(Registration, acceptedFromCandidate: false, cancellationToken);
    }

    public async Task<IActionResult> OnPostDiscoverCandidatesAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: true, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostUseCandidateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: false, cancellationToken);

        ApplyCandidateToRegistration(CandidateSelection);
        StatusMessage = $"Prefilled registration from candidate '{Registration.DisplayName}'. Review and submit to register the source.";

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptCandidateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        await DiscoverCandidatesAsync(addValidationErrorWhenEmpty: false, cancellationToken);

        ApplyCandidateToRegistration(CandidateSelection);
        return await RegisterAsync(Registration, acceptedFromCandidate: true, cancellationToken);
    }

    public async Task<IActionResult> OnPostToggleAsync(string sourceId, bool currentlyEnabled, string? category, string? search, bool? enabled, CancellationToken cancellationToken)
    {
        CategoryKey = category;
        Search = search;
        Enabled = enabled;

        try
        {
            if (currentlyEnabled)
            {
                await adminApiClient.DisableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Disabled source '{sourceId}'.";
            }
            else
            {
                await adminApiClient.EnableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Enabled source '{sourceId}'.";
            }

            return RedirectToPage(new { category = CategoryKey, search = Search, enabled = Enabled });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to toggle source {SourceId} from sources index.", sourceId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public IReadOnlyList<string> GetAssignedCategoryLabels(SourceDto source)
    {
        if (source.SupportedCategoryKeys.Count == 0)
        {
            return ["No categories assigned"];
        }

        var categoryLookup = Categories.ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);
        return source.SupportedCategoryKeys
            .Select(categoryKey => categoryLookup.TryGetValue(categoryKey, out var category) ? category.DisplayName : categoryKey)
            .ToArray();
    }

    public string BuildIntelligenceUrl(SourceDto source)
    {
        var categoryKey = !string.IsNullOrWhiteSpace(CategoryKey)
            ? CategoryKey
            : source.SupportedCategoryKeys.FirstOrDefault();

        return string.IsNullOrWhiteSpace(categoryKey)
            ? "/Sources/Intelligence"
            : $"/Sources/Intelligence?category={Uri.EscapeDataString(categoryKey)}&source={Uri.EscapeDataString(source.DisplayName)}";
    }

    public bool HasDiscoveryScaffold(SourceDto source)
    {
        return source.DiscoveryProfile.CategoryEntryPages.Count > 0
            || source.DiscoveryProfile.SitemapHints.Count > 0
            || source.DiscoveryProfile.ProductUrlPatterns.Count > 0
            || source.DiscoveryProfile.ListingUrlPatterns.Count > 0;
    }

    public bool IsBootReady(SourceDto source)
    {
        return source.IsEnabled
            && HasDiscoveryScaffold(source)
            && source.Readiness.CrawlableCategoryCount > 0;
    }

    public string GetSetupSummary(SourceDto source)
    {
        if (!source.IsEnabled)
        {
            return "Disabled until explicitly enabled.";
        }

        if (!HasDiscoveryScaffold(source))
        {
            return "No discovery seed profile is configured yet.";
        }

        if (source.Readiness.CrawlableCategoryCount == 0)
        {
            return "Assigned categories are not crawl-ready yet.";
        }

        return "Ready to seed discovery from startup defaults or an edited profile.";
    }

    public string GetCandidateRecommendationLabel(SourceCandidateDto candidate)
    {
        return candidate.RecommendationStatus switch
        {
            "recommended" => "Recommended",
            "do_not_accept" => "Do not accept",
            _ => "Manual review"
        };
    }

    public string GetCandidateRecommendationTone(SourceCandidateDto candidate)
    {
        return candidate.RecommendationStatus switch
        {
            "recommended" => "completed",
            "do_not_accept" => "warning",
            _ => "pending"
        };
    }

    public bool CanAcceptCandidate(SourceCandidateDto candidate)
    {
        return !candidate.AlreadyRegistered
            && candidate.AllowedByGovernance
            && string.Equals(candidate.RecommendationStatus, "recommended", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);
            await Task.WhenAll(categoriesTask, sourcesTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                CategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            CategoryKey = categoryContext.PrimaryCategoryKey;

            var allSources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            AllSources = allSources;
            TotalSources = allSources.Length;

            IEnumerable<SourceDto> filtered = allSources;
            if (!string.IsNullOrWhiteSpace(CategoryKey))
            {
                filtered = filtered.Where(source => source.SupportedCategoryKeys.Contains(CategoryKey, StringComparer.OrdinalIgnoreCase));
            }

            if (Enabled.HasValue)
            {
                filtered = filtered.Where(source => source.IsEnabled == Enabled.Value);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                filtered = filtered.Where(source =>
                    source.SourceId.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || source.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || source.Host.Contains(Search, StringComparison.OrdinalIgnoreCase));
            }

            Sources = filtered.ToArray();

            if (Registration.CategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
            {
                Registration.CategoryKeys = [CategoryKey];
            }

            if (CandidateDiscovery.CategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
            {
                CandidateDiscovery.CategoryKeys = [CategoryKey];
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load sources page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            AllSources = [];
            Sources = [];
        }
    }

    private static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseDelimitedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return NormalizeValues(value
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task DiscoverCandidatesAsync(bool addValidationErrorWhenEmpty, CancellationToken cancellationToken)
    {
        var categoryKeys = NormalizeValues(CandidateDiscovery.CategoryKeys);
        if (categoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
        {
            categoryKeys = [CategoryKey];
        }

        if (categoryKeys.Count == 0)
        {
            if (addValidationErrorWhenEmpty)
            {
                ModelState.AddModelError($"{nameof(CandidateDiscovery)}.{nameof(CandidateDiscovery.CategoryKeys)}", "Choose at least one category before discovering source candidates.");
            }

            CandidateDiscoveryResult = null;
            return;
        }

        CandidateDiscovery.CategoryKeys = categoryKeys;

        try
        {
            CandidateDiscoveryErrorMessage = null;
            CandidateDiscoveryResult = null;
            CandidateDiscoveryResult = await adminApiClient.DiscoverSourceCandidatesAsync(new DiscoverSourceCandidatesRequest
            {
                CategoryKeys = categoryKeys,
                Locale = NormalizeOptionalText(CandidateDiscovery.Locale),
                Market = NormalizeOptionalText(CandidateDiscovery.Market),
                BrandHints = ParseDelimitedValues(CandidateDiscovery.BrandHints),
                MaxCandidates = NormalizeMaxCandidates(CandidateDiscovery.MaxCandidates)
            }, cancellationToken);
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to discover source candidates from sources index.");
            CandidateDiscoveryErrorMessage = exception.Message;
        }
    }

    private async Task<IActionResult> RegisterAsync(RegisterSourceInput registration, bool acceptedFromCandidate, CancellationToken cancellationToken)
    {
        try
        {
            var source = await adminApiClient.RegisterSourceAsync(new RegisterSourceRequest
            {
                SourceId = registration.SourceId,
                DisplayName = registration.DisplayName,
                BaseUrl = registration.BaseUrl,
                Description = registration.Description,
                IsEnabled = registration.IsEnabled,
                AllowedMarkets = registration.AllowedMarkets,
                PreferredLocale = NormalizeOptionalText(registration.PreferredLocale),
                SupportedCategoryKeys = registration.CategoryKeys
            }, cancellationToken);

            StatusMessage = acceptedFromCandidate
                ? $"Accepted candidate '{source.DisplayName}' and registered it as a managed source. Startup discovery defaults were applied automatically."
                : $"Registered source '{source.DisplayName}'. Startup discovery defaults were applied automatically.";
            return RedirectToPage("/Sources/Details", new { sourceId = source.SourceId });
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, acceptedFromCandidate
                ? "Failed to accept candidate from sources index."
                : "Failed to register source from sources index.");
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private void ApplyCandidateToRegistration(UseCandidateInput candidate)
    {
        var categoryKeys = NormalizeValues(candidate.CategoryKeys);
        if (categoryKeys.Count == 0)
        {
            categoryKeys = NormalizeValues(CandidateDiscovery.CategoryKeys);
        }

        Registration = new RegisterSourceInput
        {
            SourceId = DeriveSourceId(candidate),
            DisplayName = candidate.DisplayName.Trim(),
            BaseUrl = NormalizeBaseUrl(candidate.BaseUrl),
            Description = Registration.Description,
            IsEnabled = candidate.IsEnabled,
            AllowedMarkets = ResolveCandidateAllowedMarkets(candidate),
            PreferredLocale = NormalizeOptionalText(candidate.PreferredLocale) ?? CandidateDiscovery.Locale ?? "en-GB",
            CategoryKeys = categoryKeys
        };
    }

    private List<string> ResolveCandidateAllowedMarkets(UseCandidateInput candidate)
    {
        var candidateMarkets = NormalizeValues(candidate.AllowedMarkets);
        if (candidateMarkets.Count > 0)
        {
            return candidateMarkets;
        }

        var discoveryMarket = NormalizeOptionalText(CandidateDiscovery.Market);
        return string.IsNullOrWhiteSpace(discoveryMarket)
            ? ["UK"]
            : [discoveryMarket];
    }

    private static string DeriveSourceId(UseCandidateInput candidate)
    {
        var candidates = new[]
        {
            TryBuildSourceIdFromBaseUrl(candidate.BaseUrl),
            NormalizeSourceId(candidate.CandidateKey),
            NormalizeSourceId(candidate.DisplayName)
        };

        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string? TryBuildSourceIdFromBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.Trim();
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        return NormalizeSourceId(host);
    }

    private static string NormalizeSourceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Trim().Length);
        var lastWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (character is '.' or '-' or '_' or ' ')
            {
                if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string NormalizeBaseUrl(string value)
    {
        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        return value.Trim();
    }

    private static int NormalizeMaxCandidates(int value)
    {
        if (value <= 0)
        {
            return 10;
        }

        return Math.Min(25, value);
    }

    public sealed class RegisterSourceInput
    {
        [Required]
        [Display(Name = "Source id")]
        public string SourceId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Url]
        [Display(Name = "Base URL")]
        public string BaseUrl { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        [Display(Name = "Allowed markets")]
        public List<string> AllowedMarkets { get; set; } = ["UK"];

        [Display(Name = "Preferred locale")]
        public string PreferredLocale { get; set; } = "en-GB";

        public List<string> CategoryKeys { get; set; } = [];
    }

    public sealed class DiscoverSourceCandidatesInput
    {
        [Display(Name = "Categories")]
        public List<string> CategoryKeys { get; set; } = [];

        [Display(Name = "Locale")]
        public string? Locale { get; set; }

        [Display(Name = "Market")]
        public string? Market { get; set; }

        [Display(Name = "Brand hints")]
        public string? BrandHints { get; set; }

        [Range(1, 25)]
        [Display(Name = "Max candidates")]
        public int MaxCandidates { get; set; } = 10;
    }

    public sealed class UseCandidateInput
    {
        public string CandidateKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public List<string> AllowedMarkets { get; set; } = [];

        public string? PreferredLocale { get; set; }

        public List<string> CategoryKeys { get; set; } = [];
    }
}