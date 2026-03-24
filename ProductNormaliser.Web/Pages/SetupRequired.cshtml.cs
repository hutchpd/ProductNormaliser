using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ProductNormaliser.Web.Options;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages;

[AllowAnonymous]
public sealed class SetupRequiredModel(
    IProductNormaliserAdminApiClient adminApiClient,
    IOptions<AdminApiOptions> adminApiOptions) : PageModel
{
    public string AdminApiBaseUrl => adminApiOptions.Value.BaseUrl;

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.GetStatsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
