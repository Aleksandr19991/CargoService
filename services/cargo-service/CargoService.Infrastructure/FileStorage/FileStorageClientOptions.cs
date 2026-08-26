namespace CargoService.Infrastructure.FileStorage;

public class FileStorageClientOptions
{
    public const string SectionName = "FileStorageService";

    public required string BaseUrl { get; init; }
}
