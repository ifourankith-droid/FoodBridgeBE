using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Listings;

/// <summary>
/// Turns a caller's <see cref="DropOffChoice"/> into the rows to write — either a delivery pointed at
/// an existing location, or a brand-new location plus its first delivery.
/// <para>
/// Shared because two different people can now be the one dropping food off: a volunteer completing a
/// delivery, and a donor delivering their own unclaimed listing. The validation rules, the error
/// wording, and the "new spots are created active and attributed to whoever found them" behaviour must
/// be identical for both — duplicating them would let the two paths drift.
/// </para>
/// </summary>
public sealed class DropOffResolver : IDropOffResolver
{
    private readonly IDropOffLocationRepository _dropOffLocationRepository;

    public DropOffResolver(IDropOffLocationRepository dropOffLocationRepository)
    {
        _dropOffLocationRepository = dropOffLocationRepository;
    }

    /// <param name="deliveredByUserId">
    /// Who physically dropped the food off — the volunteer, or the donor when self-delivering. Recorded
    /// on the delivery log and credited as the creator of any new location they discovered.
    /// </param>
    public async Task<Result<DropOffRecord>> ResolveAsync(
        Listing listing,
        DropOffChoice dropOff,
        Guid deliveredByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (dropOff.IsExisting && (dropOff.Latitude.HasValue || dropOff.Longitude.HasValue || !string.IsNullOrWhiteSpace(dropOff.Name)))
        {
            return Result.Failure<DropOffRecord>("Provide either dropOffLocationId or a new location (latitude, longitude, locationName) — not both.");
        }

        var delivery = new DropOffDelivery
        {
            // The column is named VolunteerId for historical reasons; it means "who delivered it",
            // which for a self-delivered listing is the donor.
            VolunteerId = deliveredByUserId,
            ListingId = listing.Id,
            MealsCount = listing.QuantityMeals,
            DeliveredAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };

        if (dropOff.IsExisting)
        {
            var existing = await _dropOffLocationRepository.GetByIdAsync(dropOff.LocationId!.Value, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<DropOffRecord>("The selected drop-off location does not exist.");
            }

            if (!existing.IsActive)
            {
                return Result.Failure<DropOffRecord>("The selected drop-off location is no longer active. Please choose another.");
            }

            delivery.DropOffLocationId = existing.Id;
            return Result.Success(new DropOffRecord(delivery));
        }

        // Not an existing location, so it has to be a complete new one. Report the specific missing
        // piece rather than a blanket "invalid", since this is a form someone is filling in.
        if (!dropOff.Latitude.HasValue || !dropOff.Longitude.HasValue)
        {
            return Result.Failure<DropOffRecord>("Where did you drop it off? Provide dropOffLocationId, or latitude and longitude for a new location.");
        }

        if (string.IsNullOrWhiteSpace(dropOff.Name))
        {
            return Result.Failure<DropOffRecord>("A new drop-off location needs a name.");
        }

        if (dropOff.Latitude is < -90 or > 90)
        {
            return Result.Failure<DropOffRecord>("Latitude must be between -90 and 90.");
        }

        if (dropOff.Longitude is < -180 or > 180)
        {
            return Result.Failure<DropOffRecord>("Longitude must be between -180 and 180.");
        }

        var name = dropOff.Name!.Trim();
        var newLocation = new DropOffLocation
        {
            Name = name,
            // The address is optional for a field-discovered spot — often there isn't one, and the
            // coordinates are the part that actually matters for routing.
            Address = string.IsNullOrWhiteSpace(dropOff.Address) ? name : dropOff.Address!.Trim(),
            Latitude = dropOff.Latitude.Value,
            Longitude = dropOff.Longitude.Value,
            City = null,
            IsActive = true,
            // Source stays Volunteer even when a donor found it: it distinguishes "discovered in the
            // field" from "curated by an admin", which is the distinction the UI and the admin review
            // queue actually care about.
            Source = DropOffLocationSource.Volunteer,
            CreatedByUserId = deliveredByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

        return Result.Success(new DropOffRecord(delivery, newLocation));
    }
}
