using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Dashboard.Dtos;

namespace FoodBridge.Application.Dashboard;

public static class DashboardMapper
{
    public static NearbyRecipientResponse ToResponse(this NearbyRecipient recipient) => new(
        recipient.Id,
        recipient.Name,
        recipient.Address,
        recipient.City,
        recipient.Latitude,
        recipient.Longitude,
        recipient.CapacityMeals,
        Math.Round(recipient.DistanceMeters / 1000, 2));

    public static DonorMealShareResponse ToResponse(this DonorMealShare share) => new(share.DonorId, share.DonorName, share.TotalMealsReceived);
}
