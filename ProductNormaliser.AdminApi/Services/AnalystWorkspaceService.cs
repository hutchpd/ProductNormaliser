using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Analyst;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Services;

public sealed class AnalystWorkspaceService(
    IAnalystWorkflowStore workflowStore,
    IAnalystNoteStore noteStore) : IAnalystWorkspaceService
{
    public async Task<IReadOnlyList<AnalystWorkflowDto>> GetWorkflowsAsync(string? workflowType, string? routePath, CancellationToken cancellationToken)
    {
        var workflows = await workflowStore.ListAsync(workflowType, routePath, cancellationToken);
        return workflows.Select(MapWorkflow).ToArray();
    }

    public async Task<AnalystWorkflowDto?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken)
    {
        var workflow = await workflowStore.GetAsync(workflowId, cancellationToken);
        return workflow is null ? null : MapWorkflow(workflow);
    }

    public async Task<AnalystWorkflowDto> SaveWorkflowAsync(UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkflowType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RoutePath);

        var now = DateTime.UtcNow;
        var existing = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await workflowStore.GetAsync(request.Id, cancellationToken);
        var workflow = existing ?? new AnalystWorkflow
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? $"workflow:{Guid.NewGuid():N}" : request.Id,
            CreatedUtc = now
        };

        workflow.Name = request.Name.Trim();
        workflow.WorkflowType = request.WorkflowType.Trim();
        workflow.RoutePath = request.RoutePath.Trim();
        workflow.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        workflow.PrimaryCategoryKey = string.IsNullOrWhiteSpace(request.PrimaryCategoryKey) ? null : request.PrimaryCategoryKey.Trim();
        workflow.SelectedCategoryKeys = request.SelectedCategoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        workflow.State = request.State
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        workflow.UpdatedUtc = now;

        await workflowStore.UpsertAsync(workflow, cancellationToken);
        return MapWorkflow(workflow);
    }

    public Task DeleteWorkflowAsync(string workflowId, CancellationToken cancellationToken)
        => workflowStore.DeleteAsync(workflowId, cancellationToken);

    public async Task<AnalystNoteDto?> GetNoteAsync(string targetType, string targetId, CancellationToken cancellationToken)
    {
        var note = await noteStore.GetAsync(targetType, targetId, cancellationToken);
        return note is null ? null : MapNote(note);
    }

    public async Task<AnalystNoteDto> SaveNoteAsync(UpsertAnalystNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetId);

        var existing = await noteStore.GetAsync(request.TargetType, request.TargetId, cancellationToken);
        var now = DateTime.UtcNow;
        var note = existing ?? new AnalystNote
        {
            Id = $"note:{request.TargetType.Trim().ToLowerInvariant()}:{request.TargetId.Trim()}",
            TargetType = request.TargetType.Trim(),
            TargetId = request.TargetId.Trim(),
            CreatedUtc = now
        };

        note.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        note.Content = request.Content?.Trim() ?? string.Empty;
        note.UpdatedUtc = now;

        await noteStore.UpsertAsync(note, cancellationToken);
        return MapNote(note);
    }

    public Task DeleteNoteAsync(string targetType, string targetId, CancellationToken cancellationToken)
        => noteStore.DeleteAsync(targetType, targetId, cancellationToken);

    private static AnalystWorkflowDto MapWorkflow(AnalystWorkflow workflow)
    {
        return new AnalystWorkflowDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            WorkflowType = workflow.WorkflowType,
            RoutePath = workflow.RoutePath,
            Description = workflow.Description,
            PrimaryCategoryKey = workflow.PrimaryCategoryKey,
            SelectedCategoryKeys = workflow.SelectedCategoryKeys.ToArray(),
            State = new Dictionary<string, string>(workflow.State, StringComparer.OrdinalIgnoreCase),
            CreatedUtc = workflow.CreatedUtc,
            UpdatedUtc = workflow.UpdatedUtc
        };
    }

    private static AnalystNoteDto MapNote(AnalystNote note)
    {
        return new AnalystNoteDto
        {
            TargetType = note.TargetType,
            TargetId = note.TargetId,
            Title = note.Title,
            Content = note.Content,
            CreatedUtc = note.CreatedUtc,
            UpdatedUtc = note.UpdatedUtc
        };
    }
}