using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo;

public static class MongoMappingRegistry
{
    private static int isRegistered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref isRegistered, 1) == 1)
        {
            return;
        }

        RegisterObjectSerializer();
        RegisterClassMap<AnalystNote>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<AnalystWorkflow>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<RawPage>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<CategoryMetadata>(map => map.MapIdMember(model => model.CategoryKey));
        RegisterClassMap<CrawlJob>(map => map.MapIdMember(model => model.JobId));
        RegisterClassMap<CrawlSource>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<SourceProduct>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<CanonicalProduct>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<ProductOffer>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<MergeConflict>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<CrawlQueueItem>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<CrawlLog>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<UnmappedAttribute>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<SourceQualitySnapshot>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<ProductChangeEvent>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<AdaptiveCrawlPolicy>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<SourceAttributeDisagreement>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<ManagementAuditEntry>(map => map.MapIdMember(model => model.Id));
        RegisterClassMap<CanonicalAttributeValue>();
        RegisterClassMap<AttributeEvidence>();
        RegisterClassMap<CrawlJobCategoryBreakdown>();
        RegisterClassMap<ProductSourceLink>();
        RegisterClassMap<SourceAttributeValue>();
        RegisterClassMap<NormalisedAttributeValue>();
        RegisterClassMap<UnmappedAttributeObservation>();
        RegisterClassMap<SourceThrottlingPolicy>();
    }

    private static void RegisterObjectSerializer()
    {
        BsonSerializer.RegisterSerializer(new ObjectSerializer(type =>
            ObjectSerializer.DefaultAllowedTypes(type)
            || type == typeof(decimal)
            || type == typeof(decimal?)
            || type == typeof(DateTime)
            || type == typeof(DateTime?)));
    }

    private static void RegisterClassMap<T>()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<T>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });
    }

    private static void RegisterClassMap<T>(Action<BsonClassMap<T>> configure)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<T>(map =>
        {
            map.AutoMap();
            configure(map);
            map.SetIgnoreExtraElements(true);
        });
    }
}