using Mongo2Go;
using MongoDB.Driver;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.Tests;

[SetUpFixture]
public sealed class MongoIntegrationTestFixture
{
    private MongoDbRunner? runner;

    public static MongoDbContext Context { get; private set; } = default!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        runner = MongoDbRunner.Start(singleNodeReplSet: true);
        var databaseName = $"product_normaliser_tests_{Guid.NewGuid():N}";
        var client = new MongoClient(runner.ConnectionString);
        Context = new MongoDbContext(client, databaseName);
        await Context.EnsureIndexesAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownAsync()
    {
        if (Context is not null)
        {
            await Context.Client.DropDatabaseAsync(Context.Database.DatabaseNamespace.DatabaseName);
        }

        runner?.Dispose();
    }
}