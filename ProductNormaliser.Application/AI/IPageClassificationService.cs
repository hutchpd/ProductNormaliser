namespace ProductNormaliser.Application.AI;

public interface IPageClassificationService
{
    Task<PageClassificationResult> ClassifyAsync(
        string content,
        string category,
        CancellationToken cancellationToken = default);
}
