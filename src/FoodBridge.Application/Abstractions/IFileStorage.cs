namespace FoodBridge.Application.Abstractions;

public interface IFileStorage
{
    /// <summary>
    /// Saves the content under a generated GUID filename and returns a servable
    /// relative URL (e.g. "/uploads/{guid}.jpg").
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);
}
