using MongoDB.Driver;
using ProductNormaliser.Application.Analyst;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class AnalystWorkflowRepository(MongoDbContext context)
    : MongoRepositoryBase<AnalystWorkflow>(context.AnalystWorkflows), IAnalystWorkflowStore
{
    public async Task<IReadOnlyList<AnalystWorkflow>> ListAsync(string? workflowType = null, string? routePath = null, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AnalystWorkflow>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(workflowType))
        {
            filter &= builder.Eq(workflow => workflow.WorkflowType, workflowType);
        }

        if (!string.IsNullOrWhiteSpace(routePath))
        {
            filter &= builder.Eq(workflow => workflow.RoutePath, routePath);
        }

        return await Collection
            .Find(filter)
            .SortByDescending(workflow => workflow.UpdatedUtc)
            .ThenBy(workflow => workflow.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<AnalystWorkflow?> GetAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(workflow => workflow.Id == workflowId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(AnalystWorkflow workflow, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            item => item.Id == workflow.Id,
            workflow,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        await Collection.DeleteOneAsync(workflow => workflow.Id == workflowId, cancellationToken);
    }
}