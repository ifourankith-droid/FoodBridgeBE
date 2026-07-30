using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Domain.StateMachines;

/// <summary>
/// Single source of truth for valid Listing status transitions. Recipient-reject
/// (which clears the assignment without changing status) is handled separately
/// in the Recipient module, not as a transition here.
/// <para>
/// PickedUp has two legal exits because a donation can be completed two ways:
/// via Delivered (a recipient was matched, and they confirm receipt), or straight to
/// Confirmed (no recipient was matched — the volunteer drops the food at a drop-off
/// location and their confirm-delivery completes the donation itself). Which one applies
/// is decided by whether the listing has a RecipientId, not by the caller.
/// </para>
/// </summary>
public static class ListingStateMachine
{
    private static readonly Dictionary<ListingStatus, ListingStatus[]> AllowedTransitions = new()
    {
        [ListingStatus.Pending] = new[] { ListingStatus.Claimed, ListingStatus.Cancelled, ListingStatus.Expired },
        [ListingStatus.Claimed] = new[] { ListingStatus.PickedUp, ListingStatus.Pending },
        [ListingStatus.PickedUp] = new[] { ListingStatus.Delivered, ListingStatus.Confirmed },
        [ListingStatus.Delivered] = new[] { ListingStatus.Confirmed },
    };

    public static bool CanTransition(ListingStatus from, ListingStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(ListingStatus from, ListingStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new BusinessRuleException($"Cannot transition listing from '{from}' to '{to}'.");
        }
    }
}
