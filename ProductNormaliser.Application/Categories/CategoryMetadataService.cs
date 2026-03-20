using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Application.Categories;

public sealed class CategoryMetadataService(ICategoryMetadataStore categoryMetadataStore) : ICategoryMetadataService
{
    private readonly SemaphoreSlim seedLock = new(1, 1);
    private volatile bool defaultsEnsured;

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        if (defaultsEnsured)
        {
            return;
        }

        await seedLock.WaitAsync(cancellationToken);
        try
        {
            if (defaultsEnsured)
            {
                return;
            }

            var existingCategories = await categoryMetadataStore.ListAsync(cancellationToken);
            var existingKeys = existingCategories
                .Select(category => category.CategoryKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var category in DefaultCategoryMetadataCatalog.GetAll())
            {
                if (!existingKeys.Contains(category.CategoryKey))
                {
                    await categoryMetadataStore.UpsertAsync(Normalise(category), cancellationToken);
                }
            }

            defaultsEnsured = true;
        }
        finally
        {
            seedLock.Release();
        }
    }

    public async Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);

        return (await categoryMetadataStore.ListAsync(cancellationToken))
            .Select(Normalise)
            .Where(category => !enabledOnly || category.IsEnabled)
            .OrderBy(category => category.FamilyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        await EnsureDefaultsAsync(cancellationToken);

        var category = await categoryMetadataStore.GetAsync(NormaliseKey(categoryKey), cancellationToken);
        return category is null ? null : Normalise(category);
    }

    public async Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryMetadata);

        var normalised = Normalise(categoryMetadata);
        await categoryMetadataStore.UpsertAsync(normalised, cancellationToken);
        defaultsEnsured = true;
        return normalised;
    }

    private static CategoryMetadata Normalise(CategoryMetadata category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category.CategoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.FamilyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.FamilyDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.IconKey);

        return new CategoryMetadata
        {
            CategoryKey = NormaliseKey(category.CategoryKey),
            DisplayName = category.DisplayName.Trim(),
            FamilyKey = NormaliseKey(category.FamilyKey),
            FamilyDisplayName = category.FamilyDisplayName.Trim(),
            IconKey = NormaliseKey(category.IconKey),
            CrawlSupportStatus = category.CrawlSupportStatus,
            SchemaCompletenessScore = decimal.Clamp(category.SchemaCompletenessScore, 0m, 1m),
            IsEnabled = category.IsEnabled
        };
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