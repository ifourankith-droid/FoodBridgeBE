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

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return Task.CompletedTask;
        }

        // Only ever the bare filename is used, never the caller's path. A stored URL is
        // trusted today, but treating it as untrusted costs nothing and means a future caller
        // can't turn "/uploads/../../appsettings.json" into a deletion.
        var fileName = Path.GetFileName(fileUrl);
        if (string.IsNullOrEmpty(fileName))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.Combine(_rootPath, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
