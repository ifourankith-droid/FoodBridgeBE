using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface INotificationRepository
{
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetForUserAsync(Guid userId, bool? isRead, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);
}
