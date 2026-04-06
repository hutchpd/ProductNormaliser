using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/sources/recurring-discovery-campaigns")]
public sealed class RecurringDiscoveryCampaignsController(IRecurringDiscoveryCampaignService recurringDiscoveryCampaignService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Contracts.RecurringDiscoveryCampaignDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var campaigns = await recurringDiscoveryCampaignService.ListAsync(status, cancellationToken);
        return Ok(campaigns.Select(Map).ToArray());
    }

    [HttpGet("{campaignId}")]
    [ProducesResponseType(typeof(Contracts.RecurringDiscoveryCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await recurringDiscoveryCampaignService.GetAsync(campaignId, cancellationToken);
        return campaign is null ? NotFound() : Ok(Map(campaign));
    }

    [HttpPost]
    [ProducesResponseType(typeof(Contracts.RecurringDiscoveryCampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] Contracts.CreateRecurringDiscoveryCampaignRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var campaign = await recurringDiscoveryCampaignService.CreateAsync(new CreateRecurringDiscoveryCampaignRequest
            {
                Name = request.Name,
                CategoryKeys = request.CategoryKeys,
                Locale = request.Locale,
                Market = request.Market,
                AutomationMode = request.AutomationMode,
                BrandHints = request.BrandHints,
                MaxCandidatesPerRun = request.MaxCandidatesPerRun,
                IntervalMinutes = request.IntervalMinutes
            }, cancellationToken);

            return CreatedAtAction(nameof(Get), new { campaignId = campaign.CampaignId }, Map(campaign));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(exception));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblem(exception));
        }
    }

    [HttpPost("{campaignId}/pause")]
    [ProducesResponseType(typeof(Contracts.RecurringDiscoveryCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Pause(string campaignId, CancellationToken cancellationToken = default)
        => MutateAsync(() => recurringDiscoveryCampaignService.PauseAsync(campaignId, cancellationToken));

    [HttpPost("{campaignId}/configuration")]
    [ProducesResponseType(typeof(Contracts.RecurringDiscoveryCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateConfiguration(string campaignId, [FromBody] Contracts.UpdateRecurringDiscoveryCampaignConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.IntervalMinutes is null && request.MaxCandidatesPerRun is null)
        {
            return Task.FromResult<IActionResult>(BadRequest(CreateProblem(new ArgumentException("Provide at least one campaign setting to update.", nameof(request)))));
        }

        return MutateAsync(() => recurringDiscoveryCampaignService.UpdateConfigurationAsync(campaignId, request.IntervalMinutes, request.MaxCandidatesPerRun, cancellationToken));
    }

    [HttpPost("{campaignId}/resume")]
    [ProducesResponseType(typeof(Contracts.RecurringDiscoveryCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Resume(string campaignId, CancellationToken cancellationToken = default)
        => MutateAsync(() => recurringDiscoveryCampaignService.ResumeAsync(campaignId, cancellationToken));

    [HttpDelete("{campaignId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string campaignId, CancellationToken cancellationToken = default)
    {
        return await recurringDiscoveryCampaignService.DeleteAsync(campaignId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private async Task<IActionResult> MutateAsync(Func<Task<RecurringDiscoveryCampaign?>> action)
    {
        try
        {
            var campaign = await action();
            return campaign is null ? NotFound() : Ok(Map(campaign));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(exception));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblem(exception));
        }
    }

    private static Contracts.RecurringDiscoveryCampaignDto Map(RecurringDiscoveryCampaign campaign)
    {
        return new Contracts.RecurringDiscoveryCampaignDto
        {
            CampaignId = campaign.CampaignId,
            Name = campaign.Name,
            CategoryKeys = campaign.CategoryKeys,
            Locale = campaign.Locale,
            Market = campaign.Market,
            BrandHints = campaign.BrandHints,
            AutomationMode = campaign.AutomationMode,
            MaxCandidatesPerRun = campaign.MaxCandidatesPerRun,
            IntervalMinutes = campaign.ResolveIntervalMinutes(),
            Status = campaign.Status,
            CampaignFingerprint = campaign.CampaignFingerprint,
            LastRunId = campaign.LastRunId,
            StatusMessage = campaign.StatusMessage,
            HistoricalRunCount = campaign.Memory.HistoricalRunCount,
            CompletedRunCount = campaign.Memory.CompletedRunCount,
            AcceptedCandidateCount = campaign.Memory.AcceptedCandidateCount,
            DismissedCandidateCount = campaign.Memory.DismissedCandidateCount,
            SupersededCandidateCount = campaign.Memory.SupersededCandidateCount,
            ArchivedCandidateCount = campaign.Memory.ArchivedCandidateCount,
            RunsWithAcceptedCandidates = campaign.Memory.RunsWithAcceptedCandidates,
            RunsWithoutAcceptedCandidates = campaign.Memory.RunsWithoutAcceptedCandidates,
            LastCompletedUtc = campaign.Memory.LastCompletedUtc,
            LastAcceptedUtc = campaign.Memory.LastAcceptedUtc,
            CreatedUtc = campaign.CreatedUtc,
            UpdatedUtc = campaign.UpdatedUtc,
            LastScheduledUtc = campaign.LastScheduledUtc,
            NextScheduledUtc = campaign.NextScheduledUtc
        };
    }

    private static ValidationProblemDetails CreateProblem(Exception exception)
    {
        return new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["request"] = [exception.Message]
        });
    }
}