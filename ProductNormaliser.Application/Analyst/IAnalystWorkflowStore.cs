using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Analyst;

public interface IAnalystWorkflowStore
{
    Task<IReadOnlyList<AnalystWorkflow>> ListAsync(string? workflowType = null, string? routePath = null, CancellationToken cancellationToken = default);
    Task<AnalystWorkflow?> GetAsync(string workflowId, CancellationToken cancellationToken = default);
    Task UpsertAsync(AnalystWorkflow workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(string workflowId, CancellationToken cancellationToken = default);
}