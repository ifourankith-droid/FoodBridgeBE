namespace FoodBridge.Application.Disputes.Dtos;

public sealed record DisputeResponse(
    Guid Id,
    Guid ListingId,
    Guid RaisedByUserId,
    string Reason,
    string Status,
    Guid? ResolvedByUserId,
    string? ResolutionNote,
    DateTime CreatedAtUtc);
