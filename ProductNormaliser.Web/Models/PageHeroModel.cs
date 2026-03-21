namespace ProductNormaliser.Web.Models;

public sealed class PageHeroModel
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<HeroMetricModel> Metrics { get; init; } = [];
}

public sealed class HeroMetricModel
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}