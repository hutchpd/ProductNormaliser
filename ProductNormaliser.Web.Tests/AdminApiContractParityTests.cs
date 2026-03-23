extern alias AdminApiContracts;

using System.Reflection;
using AdminContracts = AdminApiContracts::ProductNormaliser.AdminApi.Contracts;
using WebContracts = ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

public sealed class AdminApiContractParityTests
{
    private static readonly IReadOnlyList<(Type AdminType, Type WebType)> MilestoneContractPairs =
    [
        (typeof(AdminContracts.StatsResponse), typeof(WebContracts.StatsDto)),
        (typeof(AdminContracts.CategoryMetadataDto), typeof(WebContracts.CategoryMetadataDto)),
        (typeof(AdminContracts.CategoryFamilyDto), typeof(WebContracts.CategoryFamilyDto)),
        (typeof(AdminContracts.CategoryDetailDto), typeof(WebContracts.CategoryDetailDto)),
        (typeof(AdminContracts.CategorySchemaDto), typeof(WebContracts.CategorySchemaDto)),
        (typeof(AdminContracts.CategorySchemaAttributeDto), typeof(WebContracts.CategorySchemaAttributeDto)),
        (typeof(AdminContracts.SourceDto), typeof(WebContracts.SourceDto)),
        (typeof(AdminContracts.SourceThrottlingPolicyDto), typeof(WebContracts.SourceThrottlingPolicyDto)),
        (typeof(AdminContracts.RegisterSourceRequest), typeof(WebContracts.RegisterSourceRequest)),
        (typeof(AdminContracts.UpdateSourceRequest), typeof(WebContracts.UpdateSourceRequest)),
        (typeof(AdminContracts.AssignSourceCategoriesRequest), typeof(WebContracts.AssignSourceCategoriesRequest)),
        (typeof(AdminContracts.UpdateSourceThrottlingRequest), typeof(WebContracts.UpdateSourceThrottlingRequest)),
        (typeof(AdminContracts.CreateCrawlJobRequest), typeof(WebContracts.CreateCrawlJobRequest)),
        (typeof(AdminContracts.CrawlJobListResponse), typeof(WebContracts.CrawlJobListResponseDto)),
        (typeof(AdminContracts.CrawlJobDto), typeof(WebContracts.CrawlJobDto)),
        (typeof(AdminContracts.CrawlJobCategoryBreakdownDto), typeof(WebContracts.CrawlJobCategoryBreakdownDto)),
        (typeof(AdminContracts.ProductListResponse), typeof(WebContracts.ProductListResponseDto)),
        (typeof(AdminContracts.ProductSummaryResponse), typeof(WebContracts.ProductSummaryDto)),
        (typeof(AdminContracts.ProductDetailResponse), typeof(WebContracts.ProductDetailDto)),
        (typeof(AdminContracts.ProductKeyAttributeDto), typeof(WebContracts.ProductKeyAttributeDto)),
        (typeof(AdminContracts.ProductAttributeDetailDto), typeof(WebContracts.ProductAttributeDetailDto)),
        (typeof(AdminContracts.AttributeEvidenceDto), typeof(WebContracts.AttributeEvidenceDto)),
        (typeof(AdminContracts.SourceProductDetailDto), typeof(WebContracts.SourceProductDetailDto)),
        (typeof(AdminContracts.SourceAttributeValueDto), typeof(WebContracts.SourceAttributeValueDto)),
        (typeof(AdminContracts.ProductChangeEventDto), typeof(WebContracts.ProductChangeEventDto)),
        (typeof(AdminContracts.DetailedCoverageResponse), typeof(WebContracts.DetailedCoverageResponseDto)),
        (typeof(AdminContracts.AttributeCoverageDetailDto), typeof(WebContracts.AttributeCoverageDetailDto)),
        (typeof(AdminContracts.AttributeGapDto), typeof(WebContracts.AttributeGapDto)),
        (typeof(AdminContracts.UnmappedAttributeDto), typeof(WebContracts.UnmappedAttributeDto)),
        (typeof(AdminContracts.MergeInsightsResponse), typeof(WebContracts.MergeInsightsResponseDto)),
        (typeof(AdminContracts.MergeConflictInsightDto), typeof(WebContracts.MergeConflictInsightDto)),
        (typeof(AdminContracts.AttributeMappingSuggestionDto), typeof(WebContracts.AttributeMappingSuggestionDto)),
        (typeof(AdminContracts.SourceQualityScoreDto), typeof(WebContracts.SourceQualityScoreDto)),
        (typeof(AdminContracts.SourceQualitySnapshotDto), typeof(WebContracts.SourceQualitySnapshotDto)),
        (typeof(AdminContracts.AttributeStabilityDto), typeof(WebContracts.AttributeStabilityDto)),
        (typeof(AdminContracts.SourceAttributeDisagreementDto), typeof(WebContracts.SourceAttributeDisagreementDto))
    ];

    private static readonly IReadOnlyDictionary<Type, Type> AdminToWebTypeAliases = new Dictionary<Type, Type>
    {
        [typeof(AdminContracts.StatsResponse)] = typeof(WebContracts.StatsDto),
        [typeof(AdminContracts.ProductListResponse)] = typeof(WebContracts.ProductListResponseDto),
        [typeof(AdminContracts.ProductSummaryResponse)] = typeof(WebContracts.ProductSummaryDto),
        [typeof(AdminContracts.ProductDetailResponse)] = typeof(WebContracts.ProductDetailDto),
        [typeof(AdminContracts.DetailedCoverageResponse)] = typeof(WebContracts.DetailedCoverageResponseDto),
        [typeof(AdminContracts.MergeInsightsResponse)] = typeof(WebContracts.MergeInsightsResponseDto),
        [typeof(AdminContracts.CrawlJobListResponse)] = typeof(WebContracts.CrawlJobListResponseDto)
    };

    [TestCaseSource(nameof(MilestoneContractPairs))]
    public void ApiAndWebContractsStayAligned((Type AdminType, Type WebType) contractPair)
    {
        var (adminType, webType) = contractPair;
        var adminProperties = GetContractProperties(adminType);
        var webProperties = GetContractProperties(webType);

        Assert.Multiple(() =>
        {
            Assert.That(webProperties.Select(static property => property.Name),
                Is.EqualTo(adminProperties.Select(static property => property.Name)),
                $"Property names drifted between {adminType.Name} and {webType.Name}.");

            Assert.That(webProperties.Select(property => GetTypeSignature(property.PropertyType)),
                Is.EqualTo(adminProperties.Select(property => GetTypeSignature(property.PropertyType))),
                $"Property types drifted between {adminType.Name} and {webType.Name}.");
        });
    }

    private static IReadOnlyList<PropertyInfo> GetContractProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetTypeSignature(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlyingType)
        {
            return $"nullable<{GetTypeSignature(underlyingType)}>";
        }

        if (type != typeof(string) && TryGetCollectionElementType(type, out var elementType))
        {
            return $"collection<{GetTypeSignature(elementType!)}>";
        }

        if (AdminToWebTypeAliases.TryGetValue(type, out var webType))
        {
            return webType.Name;
        }

        return type.Namespace is "ProductNormaliser.Web.Contracts" or "ProductNormaliser.AdminApi.Contracts"
            ? type.Name
            : type.FullName ?? type.Name;
    }

    private static bool TryGetCollectionElementType(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface is not null)
        {
            elementType = enumerableInterface.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }
}