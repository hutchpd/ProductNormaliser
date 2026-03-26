namespace ProductNormaliser.Application.AI;

public class NoOpPageClassificationService : IPageClassificationService
{
    public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PageClassificationResult
        {
            IsProductPage = true,
            HasSpecifications = true,
            Confidence = 0.5
        });
    }
}
