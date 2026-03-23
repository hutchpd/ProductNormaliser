namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UpsertAnalystNoteRequest
{
    public string TargetType { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Content { get; init; } = string.Empty;
}