using FoodBridge.Application.Disputes.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Disputes;

public static class DisputeMapper
{
    public static DisputeResponse ToResponse(this Dispute dispute) => new(
        dispute.Id,
        dispute.ListingId,
        dispute.RaisedByUserId,
        dispute.Reason,
        dispute.Status.ToString(),
        dispute.ResolvedByUserId,
        dispute.ResolutionNote,
        dispute.CreatedAtUtc);
}
