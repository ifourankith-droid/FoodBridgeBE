using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Where a delivery was dropped, to be written in the same transaction as the delivery itself.
/// <para>
/// Bundled into one parameter rather than passed as two, because the pieces are ordered: when
/// <see cref="NewLocation"/> is set it must be inserted first so its generated id can populate
/// <see cref="Delivery"/>.<see cref="DropOffDelivery.DropOffLocationId"/>. Keeping them together
/// makes that dependency impossible to get wrong at a call site.
/// </para>
/// </summary>
/// <param name="Delivery">
/// The log row. Its <c>DropOffLocationId</c> is already set when delivering to an existing spot,
/// and filled in by the repository when <paramref name="NewLocation"/> is supplied.
/// </param>
/// <param name="NewLocation">
/// A spot the volunteer discovered in the field, to be created and then delivered to. Null when
/// delivering to a location that already exists.
/// </param>
public sealed record DropOffRecord(DropOffDelivery Delivery, DropOffLocation? NewLocation = null);
