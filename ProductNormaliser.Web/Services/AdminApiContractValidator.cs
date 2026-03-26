using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Services;

internal static class AdminApiContractValidator
{
    private static readonly IReadOnlySet<string> CandidateRecommendationStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "recommended",
        "manual_review",
        "do_not_accept"
    };

    private static readonly IReadOnlySet<string> CandidateRuntimeExtractionStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "compatible",
        "manual_review_only",
        "not_compatible"
    };

    private static readonly IReadOnlySet<string> CandidateDiscoveryDiagnosticSeverities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "info",
        "warning",
        "error"
    };

    private static readonly IReadOnlySet<string> SourceAutomationModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "operator_assisted",
        "suggest_accept",
        "auto_accept_and_seed"
    };

    private static readonly IReadOnlySet<string> SourceAutomationPostureStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "advisory",
        "healthy",
        "downgraded",
        "manual_review",
        "quarantined"
    };

    private static readonly IReadOnlySet<string> SourceAutomationActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "keep_current_mode",
        "downgrade_to_suggest",
        "flag_manual_review",
        "pause_reseeding"
    };

    private static readonly IReadOnlySet<string> SourceAutomationDecisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "manual_only",
        "suggest_accept",
        "auto_accept_and_seed"
    };

    private static readonly IReadOnlySet<string> LlmStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "disabled",
        "unconfigured",
        "load_failed",
        "runtime_failed"
    };

    private static readonly IReadOnlySet<string> ExtractionOutcomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "products_extracted",
        "no_products",
        "not_attempted"
    };

    private static readonly HashSet<string> CrawlSupportStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Planned",
        "Experimental",
        "Supported",
        "Disabled"
    };

    private static readonly HashSet<string> ConflictSensitivities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low",
        "Medium",
        "High",
        "Critical"
    };

    private static readonly HashSet<string> CrawlJobRequestTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "category",
        "source",
        "product_selection"
    };

    private static readonly HashSet<string> CrawlJobStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "running",
        "cancel_requested",
        "cancelled",
        "completed",
        "completed_with_failures",
        "failed"
    };

    private static readonly HashSet<string> CompletenessStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "complete",
        "partial",
        "sparse"
    };

    private static readonly HashSet<string> FreshnessStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "fresh",
        "aging",
        "stale"
    };

    public static void ValidateStats(StatsDto payload)
    {
    }

    public static void ValidateCategories(IReadOnlyList<CategoryMetadataDto> payload)
    {
        ValidateItems(payload, "categories", ValidateCategoryMetadata);
    }

    public static void ValidateAnalystWorkflows(IReadOnlyList<AnalystWorkflowDto> payload)
    {
        ValidateItems(payload, "analystWorkflows", ValidateAnalystWorkflow);
    }

    public static void ValidateAnalystWorkflow(AnalystWorkflowDto payload)
    {
        ValidateAnalystWorkflow(payload, "analystWorkflow");
    }

    public static void ValidateAnalystNote(AnalystNoteDto payload)
    {
        ValidateAnalystNote(payload, "analystNote");
    }

    public static void ValidateCategoryFamilies(IReadOnlyList<CategoryFamilyDto> payload)
    {
        ValidateItems(payload, "categoryFamilies", ValidateCategoryFamily);
    }

    public static void ValidateCategoryDetail(CategoryDetailDto payload)
    {
        ValidateCategoryMetadata(payload.Metadata, "categoryDetail.metadata");
        ValidateCategorySchema(payload.Schema, "categoryDetail.schema");
    }

    public static void ValidateCategorySchema(CategorySchemaDto payload)
    {
        ValidateCategorySchema(payload, "categorySchema");
    }

    public static void ValidateSources(IReadOnlyList<SourceDto> payload)
    {
        ValidateItems(payload, "sources", ValidateSource);
    }

    public static void ValidateSource(SourceDto payload)
    {
        ValidateSource(payload, "source");
    }

    public static void ValidateSourceCandidateDiscoveryResponse(SourceCandidateDiscoveryResponseDto payload)
    {
        ValidateStringItems(payload.RequestedCategoryKeys, "sourceCandidateDiscovery.requestedCategoryKeys");
        ValidateEnumValue(payload.AutomationMode, SourceAutomationModes, "sourceCandidateDiscovery.automationMode", value => value);
        ValidateEnumValue(payload.LlmStatus, LlmStatuses, "sourceCandidateDiscovery.llmStatus", value => value);
        ValidateStringItems(payload.BrandHints, "sourceCandidateDiscovery.brandHints");
        ValidateItems(payload.Diagnostics, "sourceCandidateDiscovery.diagnostics", ValidateSourceCandidateDiscoveryDiagnostic);
        ValidateItems(payload.Candidates, "sourceCandidateDiscovery.candidates", ValidateSourceCandidate);
    }

    public static void ValidateSourceOnboardingAutomationSettings(SourceOnboardingAutomationSettingsDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateEnumValue(payload.DefaultMode, SourceAutomationModes, "sourceOnboardingAutomation.defaultMode", value => value);
        ValidateEnumValue(payload.LlmStatus, LlmStatuses, "sourceOnboardingAutomation.llmStatus", value => value);
    }

    public static void ValidateCrawlJobList(CrawlJobListResponseDto payload)
    {
        ValidateItems(payload.Items, "crawlJobs.items", ValidateCrawlJob);
    }

    public static void ValidateCrawlJob(CrawlJobDto payload)
    {
        ValidateCrawlJob(payload, "crawlJob");
    }

    public static void ValidateProductList(ProductListResponseDto payload)
    {
        ValidateItems(payload.Items, "products.items", ValidateProductSummary);
    }

    public static void ValidateProductDetail(ProductDetailDto payload)
    {
        ValidateProductDetail(payload, "product");
    }

    public static void ValidateProductHistory(IReadOnlyList<ProductChangeEventDto> payload)
    {
        ValidateItems(payload, "productHistory", ValidateProductChangeEvent);
    }

    public static void ValidateDetailedCoverage(DetailedCoverageResponseDto payload)
    {
        ValidateRequiredString(payload.CategoryKey, "detailedCoverage.categoryKey");
        ValidateItems(payload.Attributes, "detailedCoverage.attributes", ValidateAttributeCoverageDetail);
        ValidateItems(payload.MostMissingAttributes, "detailedCoverage.mostMissingAttributes", ValidateAttributeGap);
        ValidateItems(payload.MostConflictedAttributes, "detailedCoverage.mostConflictedAttributes", ValidateAttributeGap);
    }

    public static void ValidateUnmappedAttributes(IReadOnlyList<UnmappedAttributeDto> payload)
    {
        ValidateItems(payload, "unmappedAttributes", ValidateUnmappedAttribute);
    }

    public static void ValidateSourceQualityScores(IReadOnlyList<SourceQualityScoreDto> payload)
    {
        ValidateItems(payload, "sourceQualityScores", ValidateSourceQualityScore);
    }

    public static void ValidateMergeInsights(MergeInsightsResponseDto payload)
    {
        ValidateRequiredString(payload.CategoryKey, "mergeInsights.categoryKey");
        ValidateItems(payload.OpenConflicts, "mergeInsights.openConflicts", ValidateMergeConflictInsight);
        ValidateItems(payload.AttributeSuggestions, "mergeInsights.attributeSuggestions", ValidateAttributeMappingSuggestion);
    }

    public static void ValidateSourceHistory(IReadOnlyList<SourceQualitySnapshotDto> payload)
    {
        ValidateItems(payload, "sourceHistory", ValidateSourceQualitySnapshot);
    }

    public static void ValidateAttributeStability(IReadOnlyList<AttributeStabilityDto> payload)
    {
        ValidateItems(payload, "attributeStability", ValidateAttributeStabilityEntry);
    }

    public static void ValidateSourceDisagreements(IReadOnlyList<SourceAttributeDisagreementDto> payload)
    {
        ValidateItems(payload, "sourceDisagreements", ValidateSourceAttributeDisagreement);
    }

    private static void ValidateCategoryMetadata(CategoryMetadataDto payload, string path)
    {
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateRequiredString(payload.FamilyKey, $"{path}.familyKey");
        ValidateRequiredString(payload.FamilyDisplayName, $"{path}.familyDisplayName");
        ValidateRequiredString(payload.IconKey, $"{path}.iconKey");
        ValidateEnumValue(payload.CrawlSupportStatus, CrawlSupportStatuses, $"{path}.crawlSupportStatus");
    }

    private static void ValidateAnalystWorkflow(AnalystWorkflowDto payload, string path)
    {
        ValidateRequiredString(payload.Id, $"{path}.id");
        ValidateRequiredString(payload.Name, $"{path}.name");
        ValidateRequiredString(payload.WorkflowType, $"{path}.workflowType");
        ValidateRequiredString(payload.RoutePath, $"{path}.routePath");
        ValidateStringItems(payload.SelectedCategoryKeys, $"{path}.selectedCategoryKeys");
    }

    private static void ValidateAnalystNote(AnalystNoteDto payload, string path)
    {
        ValidateRequiredString(payload.TargetType, $"{path}.targetType");
        ValidateRequiredString(payload.TargetId, $"{path}.targetId");
    }

    private static void ValidateCategoryFamily(CategoryFamilyDto payload, string path)
    {
        ValidateRequiredString(payload.FamilyKey, $"{path}.familyKey");
        ValidateRequiredString(payload.FamilyDisplayName, $"{path}.familyDisplayName");
        ValidateItems(payload.Categories, $"{path}.categories", ValidateCategoryMetadata);
    }

    private static void ValidateCategorySchema(CategorySchemaDto payload, string path)
    {
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateItems(payload.Attributes, $"{path}.attributes", ValidateCategorySchemaAttribute);
    }

    private static void ValidateCategorySchemaAttribute(CategorySchemaAttributeDto payload, string path)
    {
        ValidateRequiredString(payload.Key, $"{path}.key");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateRequiredString(payload.ValueType, $"{path}.valueType");
        ValidateEnumValue(payload.ConflictSensitivity, ConflictSensitivities, $"{path}.conflictSensitivity");
        ValidateRequiredString(payload.Description, $"{path}.description");
    }

    private static void ValidateSource(SourceDto payload, string path)
    {
        ValidateRequiredString(payload.SourceId, $"{path}.sourceId");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateRequiredString(payload.BaseUrl, $"{path}.baseUrl");
        ValidateRequiredString(payload.Host, $"{path}.host");
        ValidateStringItems(payload.AllowedMarkets, $"{path}.allowedMarkets");
        ValidateRequiredString(payload.PreferredLocale, $"{path}.preferredLocale");
        ValidateEnumValue(payload.AutomationPolicy.Mode, SourceAutomationModes, $"{path}.automationPolicy.mode", value => value);
        ValidateSourceDiscoveryProfile(payload.DiscoveryProfile, $"{path}.discoveryProfile");
        ValidateSourceThrottlingPolicy(payload.ThrottlingPolicy, $"{path}.throttlingPolicy");
        ValidateStringItems(payload.SupportedCategoryKeys, $"{path}.supportedCategoryKeys");
        ValidateSourceHealth(payload.Health, $"{path}.health");
        if (payload.LastActivity is not null)
        {
            ValidateSourceLastActivity(payload.LastActivity, $"{path}.lastActivity");
        }
    }

    private static void ValidateSourceHealth(SourceHealthSummaryDto payload, string path)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateRequiredString(payload.Status, $"{path}.status");
        ValidateSourceAutomationPosture(payload.Automation, $"{path}.automation");
    }

    private static void ValidateSourceAutomationPosture(SourceAutomationPostureDto payload, string path)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateEnumValue(payload.Status, SourceAutomationPostureStatuses, $"{path}.status", value => value);
        ValidateEnumValue(payload.EffectiveMode, SourceAutomationModes, $"{path}.effectiveMode", value => value);
        ValidateEnumValue(payload.RecommendedAction, SourceAutomationActions, $"{path}.recommendedAction", value => value);
        ValidateStringItems(payload.SupportingReasons, $"{path}.supportingReasons");
        ValidateStringItems(payload.BlockingReasons, $"{path}.blockingReasons");
    }

    private static void ValidateSourceDiscoveryProfile(SourceDiscoveryProfileDto payload, string path)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateStringItems(payload.AllowedMarkets, $"{path}.allowedMarkets");
        ValidateRequiredString(payload.PreferredLocale, $"{path}.preferredLocale");
        foreach (var entry in payload.CategoryEntryPages)
        {
            ValidateRequiredString(entry.Key, $"{path}.categoryEntryPages.key");
            ValidateStringItems(entry.Value, $"{path}.categoryEntryPages[{entry.Key}]");
        }

        ValidateStringItems(payload.SitemapHints, $"{path}.sitemapHints");
        ValidateStringItems(payload.AllowedHosts, $"{path}.allowedHosts");
        ValidateStringItems(payload.AllowedPathPrefixes, $"{path}.allowedPathPrefixes");
        ValidateStringItems(payload.ExcludedPathPrefixes, $"{path}.excludedPathPrefixes");
        ValidateStringItems(payload.ProductUrlPatterns, $"{path}.productUrlPatterns");
        ValidateStringItems(payload.ListingUrlPatterns, $"{path}.listingUrlPatterns");
    }

    private static void ValidateSourceCandidate(SourceCandidateDto payload, string path)
    {
        ValidateRequiredString(payload.CandidateKey, $"{path}.candidateKey");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateRequiredString(payload.BaseUrl, $"{path}.baseUrl");
        ValidateRequiredString(payload.Host, $"{path}.host");
        ValidateRequiredString(payload.CandidateType, $"{path}.candidateType");
        ValidateStringItems(payload.AllowedMarkets, $"{path}.allowedMarkets");
        ValidateRequiredString(payload.MarketEvidence, $"{path}.marketEvidence");
        ValidateRequiredString(payload.LocaleEvidence, $"{path}.localeEvidence");
        ValidateEnumValue(payload.RecommendationStatus, CandidateRecommendationStatuses, $"{path}.recommendationStatus", value => value);
        ValidateEnumValue(payload.RuntimeExtractionStatus, CandidateRuntimeExtractionStatuses, $"{path}.runtimeExtractionStatus", value => value);
        ValidateRequiredString(payload.RuntimeExtractionMessage, $"{path}.runtimeExtractionMessage");
        ValidateStringItems(payload.MatchedCategoryKeys, $"{path}.matchedCategoryKeys");
        ValidateStringItems(payload.MatchedBrandHints, $"{path}.matchedBrandHints");
        ValidateStringItems(payload.DuplicateSourceIds, $"{path}.duplicateSourceIds");
        ValidateStringItems(payload.DuplicateSourceDisplayNames, $"{path}.duplicateSourceDisplayNames");
        ValidateSourceCandidateProbe(payload.Probe, $"{path}.probe");
        ValidateSourceCandidateAutomationAssessment(payload.AutomationAssessment, $"{path}.automationAssessment");
        ValidateItems(payload.Reasons, $"{path}.reasons", ValidateSourceCandidateReason);
    }

    private static void ValidateSourceCandidateDiscoveryDiagnostic(SourceCandidateDiscoveryDiagnosticDto payload, string path)
    {
        ValidateRequiredString(payload.Code, $"{path}.code");
        ValidateEnumValue(payload.Severity, CandidateDiscoveryDiagnosticSeverities, $"{path}.severity", value => value);
        ValidateRequiredString(payload.Title, $"{path}.title");
        ValidateRequiredString(payload.Message, $"{path}.message");
    }

    private static void ValidateSourceCandidateAutomationAssessment(SourceCandidateAutomationAssessmentDto payload, string path)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateEnumValue(payload.RequestedMode, SourceAutomationModes, $"{path}.requestedMode", value => value);
        ValidateEnumValue(payload.Decision, SourceAutomationDecisions, $"{path}.decision", value => value);
        ValidateRequiredString(payload.MarketEvidence, $"{path}.marketEvidence");
        ValidateRequiredString(payload.LocaleEvidence, $"{path}.localeEvidence");
        ValidateStringItems(payload.SupportingReasons, $"{path}.supportingReasons");
        ValidateStringItems(payload.BlockingReasons, $"{path}.blockingReasons");
    }

    private static void ValidateSourceCandidateProbe(SourceCandidateProbeDto payload, string path)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateStringItems(payload.SitemapUrls, $"{path}.sitemapUrls");
        ValidateStringItems(payload.CategoryPageHints, $"{path}.categoryPageHints");
        ValidateStringItems(payload.LikelyListingUrlPatterns, $"{path}.likelyListingUrlPatterns");
        ValidateStringItems(payload.LikelyProductUrlPatterns, $"{path}.likelyProductUrlPatterns");
    }

    private static void ValidateSourceCandidateReason(SourceCandidateReasonDto payload, string path)
    {
        ValidateRequiredString(payload.Code, $"{path}.code");
        ValidateRequiredString(payload.Message, $"{path}.message");
    }

    private static void ValidateSourceThrottlingPolicy(SourceThrottlingPolicyDto payload, string path)
    {
        ArgumentNullException.ThrowIfNull(payload);
    }

    private static void ValidateSourceLastActivity(SourceLastActivityDto payload, string path)
    {
        ValidateRequiredString(payload.Status, $"{path}.status");
        ValidateEnumValue(payload.ExtractionOutcome, ExtractionOutcomes, $"{path}.extractionOutcome", value => value);
    }

    private static void ValidateCrawlJob(CrawlJobDto payload, string path)
    {
        ValidateRequiredString(payload.JobId, $"{path}.jobId");
        ValidateEnumValue(payload.RequestType, CrawlJobRequestTypes, $"{path}.requestType", NormalizeLowerUnderscore);
        ValidateEnumValue(payload.Status, CrawlJobStatuses, $"{path}.status", NormalizeLowerUnderscore);
        ValidateStringItems(payload.RequestedCategories, $"{path}.requestedCategories");
        ValidateStringItems(payload.RequestedSources, $"{path}.requestedSources");
        ValidateStringItems(payload.RequestedProductIds, $"{path}.requestedProductIds");
        ValidateItems(payload.PerCategoryBreakdown, $"{path}.perCategoryBreakdown", ValidateCrawlJobCategoryBreakdown);
    }

    private static void ValidateCrawlJobCategoryBreakdown(CrawlJobCategoryBreakdownDto payload, string path)
    {
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
    }

    private static void ValidateProductSummary(ProductSummaryDto payload, string path)
    {
        ValidateRequiredString(payload.Id, $"{path}.id");
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.Brand, $"{path}.brand");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateEnumValue(payload.CompletenessStatus, CompletenessStatuses, $"{path}.completenessStatus", NormalizeLowerUnderscore);
        ValidateEnumValue(payload.FreshnessStatus, FreshnessStatuses, $"{path}.freshnessStatus", NormalizeLowerUnderscore);
        ValidateItems(payload.KeyAttributes, $"{path}.keyAttributes", ValidateProductKeyAttribute);
    }

    private static void ValidateProductDetail(ProductDetailDto payload, string path)
    {
        ValidateRequiredString(payload.Id, $"{path}.id");
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.Brand, $"{path}.brand");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateEnumValue(payload.CompletenessStatus, CompletenessStatuses, $"{path}.completenessStatus", NormalizeLowerUnderscore);
        ValidateEnumValue(payload.FreshnessStatus, FreshnessStatuses, $"{path}.freshnessStatus", NormalizeLowerUnderscore);
        ValidateItems(payload.KeyAttributes, $"{path}.keyAttributes", ValidateProductKeyAttribute);
        ValidateItems(payload.Attributes, $"{path}.attributes", ValidateProductAttributeDetail);
        ValidateItems(payload.SourceProducts, $"{path}.sourceProducts", ValidateSourceProductDetail);
    }

    private static void ValidateProductKeyAttribute(ProductKeyAttributeDto payload, string path)
    {
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
        ValidateRequiredString(payload.Value, $"{path}.value");
    }

    private static void ValidateProductAttributeDetail(ProductAttributeDetailDto payload, string path)
    {
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.ValueType, $"{path}.valueType");
        ValidateItems(payload.Evidence, $"{path}.evidence", ValidateAttributeEvidence);
    }

    private static void ValidateAttributeEvidence(AttributeEvidenceDto payload, string path)
    {
        ValidateRequiredString(payload.SourceName, $"{path}.sourceName");
        ValidateRequiredString(payload.SourceUrl, $"{path}.sourceUrl");
        ValidateRequiredString(payload.SourceProductId, $"{path}.sourceProductId");
        ValidateRequiredString(payload.SourceAttributeKey, $"{path}.sourceAttributeKey");
    }

    private static void ValidateSourceProductDetail(SourceProductDetailDto payload, string path)
    {
        ValidateRequiredString(payload.Id, $"{path}.id");
        ValidateRequiredString(payload.SourceName, $"{path}.sourceName");
        ValidateRequiredString(payload.SourceUrl, $"{path}.sourceUrl");
        ValidateRequiredString(payload.RawSchemaJson, $"{path}.rawSchemaJson");
        ValidateItems(payload.RawAttributes, $"{path}.rawAttributes", ValidateSourceAttributeValue);
    }

    private static void ValidateSourceAttributeValue(SourceAttributeValueDto payload, string path)
    {
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.ValueType, $"{path}.valueType");
    }

    private static void ValidateProductChangeEvent(ProductChangeEventDto payload, string path)
    {
        ValidateRequiredString(payload.CanonicalProductId, $"{path}.canonicalProductId");
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.SourceName, $"{path}.sourceName");
    }

    private static void ValidateAttributeCoverageDetail(AttributeCoverageDetailDto payload, string path)
    {
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
    }

    private static void ValidateAttributeGap(AttributeGapDto payload, string path)
    {
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.DisplayName, $"{path}.displayName");
    }

    private static void ValidateUnmappedAttribute(UnmappedAttributeDto payload, string path)
    {
        ValidateRequiredString(payload.CanonicalKey, $"{path}.canonicalKey");
        ValidateRequiredString(payload.RawAttributeKey, $"{path}.rawAttributeKey");
        ValidateStringItems(payload.SourceNames, $"{path}.sourceNames");
        ValidateStringItems(payload.SampleValues, $"{path}.sampleValues");
    }

    private static void ValidateSourceQualityScore(SourceQualityScoreDto payload, string path)
    {
        ValidateRequiredString(payload.SourceName, $"{path}.sourceName");
    }

    private static void ValidateMergeConflictInsight(MergeConflictInsightDto payload, string path)
    {
        ValidateRequiredString(payload.Id, $"{path}.id");
        ValidateRequiredString(payload.CanonicalProductId, $"{path}.canonicalProductId");
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
        ValidateRequiredString(payload.Reason, $"{path}.reason");
    }

    private static void ValidateAttributeMappingSuggestion(AttributeMappingSuggestionDto payload, string path)
    {
        ValidateRequiredString(payload.RawAttributeKey, $"{path}.rawAttributeKey");
        ValidateRequiredString(payload.SuggestedCanonicalKey, $"{path}.suggestedCanonicalKey");
        ValidateStringItems(payload.SourceNames, $"{path}.sourceNames");
    }

    private static void ValidateSourceQualitySnapshot(SourceQualitySnapshotDto payload, string path)
    {
        ValidateRequiredString(payload.SourceName, $"{path}.sourceName");
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
    }

    private static void ValidateAttributeStabilityEntry(AttributeStabilityDto payload, string path)
    {
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
    }

    private static void ValidateSourceAttributeDisagreement(SourceAttributeDisagreementDto payload, string path)
    {
        ValidateRequiredString(payload.SourceName, $"{path}.sourceName");
        ValidateRequiredString(payload.CategoryKey, $"{path}.categoryKey");
        ValidateRequiredString(payload.AttributeKey, $"{path}.attributeKey");
    }

    private static void ValidateRequiredString(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AdminApiException($"Admin API response field '{path}' was missing or empty.");
        }
    }

    private static void ValidateStringItems(IEnumerable<string> values, string path)
    {
        var index = 0;
        foreach (var value in values)
        {
            ValidateRequiredString(value, $"{path}[{index}]");
            index++;
        }
    }

    private static void ValidateItems<T>(IEnumerable<T> items, string path, Action<T, string> validate)
    {
        var index = 0;
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new AdminApiException($"Admin API response field '{path}[{index}]' was null.");
            }

            validate(item, $"{path}[{index}]");
            index++;
        }
    }

    private static void ValidateEnumValue(string? value, IReadOnlySet<string> allowedValues, string path, Func<string, string>? normalizer = null)
    {
        ValidateRequiredString(value, path);

        var candidate = normalizer is null ? value! : normalizer(value!);
        if (!allowedValues.Contains(candidate))
        {
            throw new AdminApiException($"Admin API response field '{path}' had unsupported value '{value}'.");
        }
    }

    private static string NormalizeLowerUnderscore(string value)
    {
        return value.Trim().Replace('-', '_').ToLowerInvariant();
    }
}