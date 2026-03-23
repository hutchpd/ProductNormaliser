using ProductNormaliser.AdminApi.Contracts;

namespace ProductNormaliser.AdminApi.Services;

public interface IAnalystWorkspaceService
{
    Task<IReadOnlyList<AnalystWorkflowDto>> GetWorkflowsAsync(string? workflowType, string? routePath, CancellationToken cancellationToken);
    Task<AnalystWorkflowDto?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken);
    Task<AnalystWorkflowDto> SaveWorkflowAsync(UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken);
    Task DeleteWorkflowAsync(string workflowId, CancellationToken cancellationToken);
    Task<AnalystNoteDto?> GetNoteAsync(string targetType, string targetId, CancellationToken cancellationToken);
    Task<AnalystNoteDto> SaveNoteAsync(UpsertAnalystNoteRequest request, CancellationToken cancellationToken);
    Task DeleteNoteAsync(string targetType, string targetId, CancellationToken cancellationToken);
}