using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Services;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/analyst-workspace")]
public sealed class AnalystWorkspaceController(IAnalystWorkspaceService analystWorkspaceService) : ControllerBase
{
    [HttpGet("workflows")]
    public async Task<IActionResult> GetWorkflows([FromQuery] string? workflowType = null, [FromQuery] string? routePath = null, CancellationToken cancellationToken = default)
    {
        return Ok(await analystWorkspaceService.GetWorkflowsAsync(workflowType, routePath, cancellationToken));
    }

    [HttpGet("workflows/{workflowId}")]
    public async Task<IActionResult> GetWorkflow(string workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await analystWorkspaceService.GetWorkflowAsync(workflowId, cancellationToken);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost("workflows")]
    public async Task<IActionResult> SaveWorkflow([FromBody] UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await analystWorkspaceService.SaveWorkflowAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(exception.ParamName ?? string.Empty, exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpDelete("workflows/{workflowId}")]
    public async Task<IActionResult> DeleteWorkflow(string workflowId, CancellationToken cancellationToken = default)
    {
        await analystWorkspaceService.DeleteWorkflowAsync(workflowId, cancellationToken);
        return NoContent();
    }

    [HttpGet("notes")]
    public async Task<IActionResult> GetNote([FromQuery] string targetType, [FromQuery] string targetId, CancellationToken cancellationToken = default)
    {
        var note = await analystWorkspaceService.GetNoteAsync(targetType, targetId, cancellationToken);
        return note is null ? NotFound() : Ok(note);
    }

    [HttpPost("notes")]
    public async Task<IActionResult> SaveNote([FromBody] UpsertAnalystNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await analystWorkspaceService.SaveNoteAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(exception.ParamName ?? string.Empty, exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpDelete("notes")]
    public async Task<IActionResult> DeleteNote([FromQuery] string targetType, [FromQuery] string targetId, CancellationToken cancellationToken = default)
    {
        await analystWorkspaceService.DeleteNoteAsync(targetType, targetId, cancellationToken);
        return NoContent();
    }
}