using MongoDB.Bson;
using MongoDB.Driver;

namespace ProductNormaliser.Tests;

internal static class MongoDatabaseExtensions
{
    public static async Task DropCollectionIfExistsAsync(this IMongoDatabase database, string collectionName)
    {
        var filter = new BsonDocument("name", collectionName);
        using var cursor = await database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = filter });
        if (await cursor.AnyAsync())
        {
            await database.DropCollectionAsync(collectionName);
        }
    }
}