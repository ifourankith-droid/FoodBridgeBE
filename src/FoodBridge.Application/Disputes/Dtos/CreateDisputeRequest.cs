namespace FoodBridge.Application.Disputes.Dtos;

public sealed record CreateDisputeRequest(Guid ListingId, string Reason);
