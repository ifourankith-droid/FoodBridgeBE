using FoodBridge.Application.Abstractions;

namespace FoodBridge.Infrastructure.Storage;

/// <summary>
/// Writes files to a local directory (dev/hackathon scope). A Cloudinary or
/// S3 implementation can replace this later without touching any consumer,
/// since everything depends on <see cref="IFileStorage"/> only.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly string _urlPrefix;

    public LocalFileStorage(string rootPath, string urlPrefix)
    {
        _rootPath = rootPath;
        _urlPrefix = urlPrefix.TrimEnd('/');
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default)
    {
        var fileName = $"{Guid.NewGuid()}{fileExtension}";
        var fullPath = Path.Combine(_rootPath, fileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"{_urlPrefix}/{fileName}";
    }
}
