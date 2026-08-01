using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Dashboard.Dtos;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private const double NearbyRadiusKm = 10;
    private const int NearbyLimit = 5;
    private const int RecentActivityLimit = 5;
    private const int DonorDistributionLimit = 5;

    private const int TenDeliveriesThreshold = 10;
    private const int HundredMealsThreshold = 100;

    private readonly IReportsReader _reportsReader;
    private readonly IDashboardReader _dashboardReader;
    private readonly ILeaderboardReader _leaderboardReader;
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DashboardService(
        IReportsReader reportsReader,
        IDashboardReader dashboardReader,
        ILeaderboardReader leaderboardReader,
        IListingRepository listingRepository,
        IUserRepository userRepository,
        ICurrentUser currentUser,
        IClock clock)
    {
        _reportsReader = reportsReader;
        _dashboardReader = dashboardReader;
        _leaderboardReader = leaderboardReader;
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<DonorDashboardResponse>> GetDonorDashboardAsync(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCoordinates(latitude, longitude);
        if (validationError is not null)
        {
            return Result.Failure<DonorDashboardResponse>(validationError);
        }

        var donorId = _currentUser.UserId;

        var (totalListings, totalMealsDonated, totalCertificates) = await _reportsReader.GetDonorSummaryAsync(donorId, cancellationToken);
        var byMonth = await _reportsReader.GetDonorMealsByMonthAsync(donorId, cancellationToken);
        var mealsToday = await _dashboardReader.GetDonorMealsDonatedTodayAsync(donorId, _clock.UtcNow, cancellationToken);

        var (recentListings, _) = await _listingRepository.GetByDonorAsync(donorId, null, null, null, 1, RecentActivityLimit, cancellationToken);
        var recentActivity = recentListings.Select(l => l.ToSummaryResponse()).ToList();

        var nearbyRecipients = await ResolveNearbyRecipientsAsync(donorId, latitude, longitude, cancellationToken);

        return Result.Success(new DonorDashboardResponse(totalMealsDonated, mealsToday, totalListings, totalCertificates, byMonth, recentActivity, nearbyRecipients));
    }

    public async Task<Result<VolunteerDashboardResponse>> GetVolunteerDashboardAsync(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCoordinates(latitude, longitude);
        if (validationError is not null)
        {
            return Result.Failure<VolunteerDashboardResponse>(validationError);
        }

        var volunteerId = _currentUser.UserId;

        var (totalDeliveries, totalPoints) = await _reportsReader.GetVolunteerSummaryAsync(volunteerId, cancellationToken);
        var byMonth = await _reportsReader.GetVolunteerDeliveriesByMonthAsync(volunteerId, cancellationToken);
        var mealsHelped = await _dashboardReader.GetVolunteerMealsHelpedAsync(volunteerId, cancellationToken);
        var leaderboardEntry = await _leaderboardReader.GetForVolunteerAsync(volunteerId, cancellationToken);
        var badges = BuildBadges(totalDeliveries, mealsHelped);

        var openListingsNearby = await ResolveOpenListingsNearbyAsync(volunteerId, latitude, longitude, cancellationToken);

        return Result.Success(new VolunteerDashboardResponse(totalDeliveries, totalPoints, leaderboardEntry?.Rank, mealsHelped, byMonth, badges, openListingsNearby));
    }

    public async Task<Result<RecipientDashboardResponse>> GetRecipientDashboardAsync(CancellationToken cancellationToken = default)
    {
        var recipientId = _currentUser.UserId;

        var (totalMealsReceived, totalDeliveriesReceived) = await _reportsReader.GetRecipientSummaryAsync(recipientId, cancellationToken);
        var byMonth = await _reportsReader.GetRecipientMealsByMonthAsync(recipientId, cancellationToken);
        var mealsToday = await _dashboardReader.GetRecipientMealsReceivedTodayAsync(recipientId, _clock.UtcNow, cancellationToken);
        var distribution = await _dashboardReader.GetRecipientDonorDistributionAsync(recipientId, DonorDistributionLimit, cancellationToken);

        var (incomingItems, incomingTotalCount) = await _listingRepository.GetIncomingForRecipientAsync(recipientId, 1, RecentActivityLimit, cancellationToken);
        var incomingFood = incomingItems.Select(l => l.ToSummaryResponse()).ToList();

        var recipient = await _userRepository.GetByIdAsync(recipientId, cancellationToken);
        var capacity = recipient?.CapacityMeals;
        var usedPercent = capacity is > 0 ? Math.Round(mealsToday * 100.0 / capacity.Value, 1) : (double?)null;

        return Result.Success(new RecipientDashboardResponse(
            totalMealsReceived,
            totalDeliveriesReceived,
            mealsToday,
            incomingTotalCount,
            capacity,
            usedPercent,
            byMonth,
            distribution.Select(d => d.ToResponse()).ToList(),
            incomingFood));
    }

    private async Task<IReadOnlyList<NearbyRecipientResponse>> ResolveNearbyRecipientsAsync(Guid donorId, decimal? latitude, decimal? longitude, CancellationToken cancellationToken)
    {
        var (lat, lng) = await ResolveCoordinatesAsync(donorId, latitude, longitude, cancellationToken);
        if (lat is null || lng is null)
        {
            return Array.Empty<NearbyRecipientResponse>();
        }

        var nearby = await _dashboardReader.GetNearbyRecipientsAsync(lat.Value, lng.Value, NearbyRadiusKm * 1000, NearbyLimit, cancellationToken);
        return nearby.Select(r => r.ToResponse()).ToList();
    }

    private async Task<IReadOnlyList<ListingNearbyResponse>> ResolveOpenListingsNearbyAsync(Guid volunteerId, decimal? latitude, decimal? longitude, CancellationToken cancellationToken)
    {
        var (lat, lng) = await ResolveCoordinatesAsync(volunteerId, latitude, longitude, cancellationToken);
        if (lat is null || lng is null)
        {
            return Array.Empty<ListingNearbyResponse>();
        }

        var (items, _) = await _listingRepository.GetNearbyPendingAsync(lat.Value, lng.Value, NearbyRadiusKm * 1000, null, null, ListingStatus.Pending, 1, NearbyLimit, cancellationToken);
        return items.Select(i => i.ToResponse()).ToList();
    }

    private async Task<(decimal? Latitude, decimal? Longitude)> ResolveCoordinatesAsync(Guid userId, decimal? latitude, decimal? longitude, CancellationToken cancellationToken)
    {
        if (latitude.HasValue && longitude.HasValue)
        {
            return (latitude, longitude);
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return (user?.Latitude, user?.Longitude);
    }

    private static string? ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude is < -90 or > 90)
        {
            return "Latitude must be between -90 and 90.";
        }

        if (longitude is < -180 or > 180)
        {
            return "Longitude must be between -180 and 180.";
        }

        return null;
    }

    private static IReadOnlyList<BadgeResponse> BuildBadges(int totalDeliveries, int totalMealsHelped) => new[]
    {
        new BadgeResponse("FirstDelivery", "First Delivery", totalDeliveries >= 1),
        new BadgeResponse("TenDeliveries", "10 Deliveries", totalDeliveries >= TenDeliveriesThreshold),
        new BadgeResponse("HundredMeals", "100 Meals", totalMealsHelped >= HundredMealsThreshold),
    };
}
