namespace FoodBridge.Application.Listings.Dtos;

/// <summary>
/// The volunteer's answer to "where did you drop it off?" on confirm-delivery. Exactly one of
/// the two forms must be supplied — an existing <paramref name="LocationId"/>, or the details of
/// a new spot they found. Both, or neither, is a 422: silently preferring one over the other
/// would ignore something the caller explicitly sent, the same reasoning
/// <c>CreateListingRequestValidator</c> applies to <c>DonorAddressId</c> vs the freeform address.
/// <para>
/// Assembled by the controller from individual <c>[FromForm]</c> scalars rather than being bound
/// as a DTO: this endpoint is <c>multipart/form-data</c>, and per the Phase 13 decision log,
/// model-binding a complex type alongside <c>IFormFile</c> on these actions has bitten this
/// project before.
/// </para>
/// </summary>
/// <param name="LocationId">An existing drop-off location the volunteer delivered to.</param>
/// <param name="Latitude">New spot's latitude, from the volunteer's device.</param>
/// <param name="Longitude">New spot's longitude.</param>
/// <param name="Name">New spot's short label, e.g. "Paldi underbridge".</param>
/// <param name="Address">New spot's address; falls back to the name when omitted.</param>
public sealed record DropOffChoice(
    Guid? LocationId,
    decimal? Latitude,
    decimal? Longitude,
    string? Name,
    string? Address)
{
    /// <summary>True when the volunteer picked a spot that already exists.</summary>
    public bool IsExisting => LocationId.HasValue;

    /// <summary>True when the volunteer is recording a spot the platform hasn't seen before.</summary>
    public bool IsNew => !LocationId.HasValue
        && Latitude.HasValue
        && Longitude.HasValue
        && !string.IsNullOrWhiteSpace(Name);
}
