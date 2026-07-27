namespace FoodBridge.Application.Dashboard.Dtos;

/// <summary>
/// Computed from existing stats, not a persisted achievements table — only badges with an
/// objective, data-backed definition are included (First Delivery / 10 Deliveries / 100 Meals).
/// The prototype's "Speed Runner" and "Night Owl" badges have no measurable criteria anywhere
/// in the domain model (no delivery-speed or time-of-day tracking exists), so they're omitted
/// rather than invented.
/// </summary>
public sealed record BadgeResponse(string Code, string Name, bool Earned);
