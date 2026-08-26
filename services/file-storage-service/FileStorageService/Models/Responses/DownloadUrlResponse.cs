namespace FileStorageService.API.Models.Responses;

public sealed record DownloadUrlResponse
{
    public required Guid FileId { get; init; }
    public required string DownloadUrl { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
