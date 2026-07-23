using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Listings;

public static class ListingMapper
{
    public static ListingResponse ToResponse(this Listing listing, IReadOnlyList<ListingImage> images, IReadOnlyList<ListingTimelineEvent> timeline) => new(
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
        listing.CreatedAtUtc,
        listing.UpdatedAtUtc,
        images.Select(i => new ListingImageResponse(i.Id, i.ImageUrl, i.CreatedAtUtc)).ToList(),
        timeline.Select(t => new ListingTimelineEntryResponse(t.FromStatus?.ToString(), t.ToStatus.ToString(), t.ActorUserId, t.Note, t.PhotoUrl, t.CreatedAtUtc)).ToList());

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
