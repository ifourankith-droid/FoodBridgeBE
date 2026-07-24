using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Tracking.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Tracking;

public sealed class TrackingService : ITrackingService
{
    private readonly IListingRepository _listingRepository;
    private readonly ITrackingStore _trackingStore;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public TrackingService(IListingRepository listingRepository, ITrackingStore trackingStore, ICurrentUser currentUser, IClock clock)
    {
        _listingRepository = listingRepository;
        _trackingStore = trackingStore;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<TrackingResponse?>> GetTrackingAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetListingOrThrowAsync(listingId, cancellationToken);
        EnsureRelatedToListing(listing);

        var location = _trackingStore.GetLocation(listingId);
        var response = location is null ? null : new TrackingResponse(listingId, location.Latitude, location.Longitude, location.ReportedAtUtc);
        return Result.Success(response, response is null ? "No location has been reported for this listing yet." : "Success");
    }

    public async Task<Result<TrackingResponse>> ReportLocationAsync(Guid listingId, decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        var listing = await GetListingOrThrowAsync(listingId, cancellationToken);
        if (listing.VolunteerId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("Only the assigned volunteer can report location for this listing.");
        }

        var now = _clock.UtcNow;
        _trackingStore.SetLocation(listingId, latitude, longitude, now);
        return Result.Success(new TrackingResponse(listingId, latitude, longitude, now));
    }

    private async Task<Listing> GetListingOrThrowAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        return listing;
    }

    private void EnsureRelatedToListing(Listing listing)
    {
        var userId = _currentUser.UserId;
        if (listing.DonorId != userId && listing.VolunteerId != userId && listing.RecipientId != userId)
        {
            throw new UnauthorizedAccessException("You can only track listings you're involved in.");
        }
    }
}
