using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Listings;

public static class ListingMapper
{
    /// <summary>
    /// Builds the full detail response, including the donor's and (once assigned) the
    /// matched volunteer's/recipient's name and mobile, so the parties on a listing can
    /// coordinate the physical handoff. Every caller already restricts access to the
    /// listing's own donor/volunteer/recipient (Admin's browse view uses a separate,
    /// narrower summary DTO instead) — so this never leaks contact info to an uninvolved user.
    /// </summary>
    public static async Task<ListingResponse> ToResponseAsync(
        this Listing listing,
        IReadOnlyList<ListingImage> images,
        IReadOnlyList<ListingTimelineEvent> timeline,
        IUserRepository userRepository,
        CancellationToken cancellationToken = default)
    {
        var donor = await userRepository.GetByIdAsync(listing.DonorId, cancellationToken);
        var volunteer = listing.VolunteerId.HasValue ? await userRepository.GetByIdAsync(listing.VolunteerId.Value, cancellationToken) : null;
        var recipient = listing.RecipientId.HasValue ? await userRepository.GetByIdAsync(listing.RecipientId.Value, cancellationToken) : null;

        return new ListingResponse(
            listing.Id,
            listing.DonorId,
            listing.Title,
            listing.FoodType,
            listing.DietType?.ToString(),
            listing.MealType?.ToString(),
            listing.QuantityMeals,
            listing.FreshnessTag.ToString(),
            listing.PreparedAtUtc,
            listing.PickupDeadlineUtc,
            listing.PickupAddress,
            listing.Latitude,
            listing.Longitude,
            listing.Status.ToString(),
            listing.VolunteerId,
            listing.RecipientId,
            donor?.Name ?? string.Empty,
            donor?.Mobile ?? string.Empty,
            volunteer?.Name,
            volunteer?.Mobile,
            recipient?.Name,
            recipient?.Mobile,
            listing.CreatedAtUtc,
            listing.UpdatedAtUtc,
            images.Select(i => new ListingImageResponse(i.Id, i.ImageUrl, i.CreatedAtUtc)).ToList(),
            timeline.Select(t => new ListingTimelineEntryResponse(t.FromStatus?.ToString(), t.ToStatus.ToString(), t.ActorUserId, t.Note, t.PhotoUrl, t.CreatedAtUtc)).ToList());
    }

    public static ListingSummaryResponse ToSummaryResponse(this Listing listing) => new(
        listing.Id,
        listing.Title,
        listing.FoodType,
        listing.DietType?.ToString(),
        listing.MealType?.ToString(),
        listing.QuantityMeals,
        listing.FreshnessTag.ToString(),
        listing.PickupDeadlineUtc,
        listing.Status.ToString(),
        listing.CreatedAtUtc);
}
