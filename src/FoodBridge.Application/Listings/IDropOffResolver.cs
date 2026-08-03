using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Listings;

/// <summary>
/// Validates a drop-off choice and builds the rows to persist. See <see cref="DropOffResolver"/>.
/// </summary>
public interface IDropOffResolver
{
    Task<Result<DropOffRecord>> ResolveAsync(
        Listing listing,
        DropOffChoice dropOff,
        Guid deliveredByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
