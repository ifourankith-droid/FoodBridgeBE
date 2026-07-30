namespace FoodBridge.Application.Abstractions;

public interface IFileStorage
{
    /// <summary>
    /// Saves the content under a generated GUID filename and returns a servable
    /// relative URL (e.g. "/uploads/{guid}.jpg").
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously saved file by the URL <see cref="SaveAsync"/> returned. Best-effort and
    /// idempotent: a missing or already-deleted file is not an error, since the only caller is
    /// cleanup after a replacement that has *already* been committed — failing there would undo a
    /// successful upload to tidy up a stray file, which is the wrong trade.
    /// </summary>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
}
