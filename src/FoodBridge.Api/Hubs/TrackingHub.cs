using FoodBridge.Application.Tracking;
using FoodBridge.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FoodBridge.Api.Hubs;

/// <summary>
/// Live delivery tracking. Donor/volunteer/recipient clients join a per-listing group
/// to receive position updates; the assigned volunteer's client calls
/// <see cref="UpdateLocation"/> periodically while en route.
/// </summary>
[Authorize]
public sealed class TrackingHub : Hub
{
    private readonly ITrackingService _trackingService;

    public TrackingHub(ITrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    public Task JoinTracking(Guid listingId) => HandleExceptionsAsync(async () =>
    {
        var result = await _trackingService.GetTrackingAsync(listingId);
        if (!result.IsSuccess)
        {
            throw new HubException(result.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(listingId));
        return true;
    });

    public Task LeaveTracking(Guid listingId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(listingId));

    public Task UpdateLocation(Guid listingId, decimal latitude, decimal longitude) => HandleExceptionsAsync(async () =>
    {
        var result = await _trackingService.ReportLocationAsync(listingId, latitude, longitude);
        if (!result.IsSuccess)
        {
            throw new HubException(result.Message);
        }

        await Clients.Group(GroupName(listingId)).SendAsync("LocationUpdated", result.Data);
        return true;
    });

    public static string GroupName(Guid listingId) => $"listing:{listingId}";

    private static async Task<T> HandleExceptionsAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (NotFoundException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("You are not authorized to perform this action.");
        }
        catch (BusinessRuleException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}
