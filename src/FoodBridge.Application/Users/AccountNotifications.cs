using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Users;

/// <summary>
/// Notifications about a user's own account status, as opposed to a listing's lifecycle
/// (<see cref="Listings.ListingNotifications"/>). Wording and <c>Type</c> strings live here for the
/// same reason: they're a contract with the frontend's per-type icon/destination registry.
/// <para>
/// Unlike the listing types, <c>AccountVerified</c> can reach more than one role (a Volunteer or a
/// Recipient), so its frontend destination must be a view every role can open — the dashboard. Don't
/// point it at a role-specific page, or it becomes unroutable for the other role.
/// </para>
/// </summary>
public static class AccountNotifications
{
    /// <summary>
    /// An admin approved this account. Distinguishes a first approval from a reinstatement, because
    /// "you're verified" reads as nonsense to someone who was suspended an hour ago and knows it.
    /// </summary>
    /// <param name="previousStatus">Status before the change, used only to pick the wording.</param>
    public static Notification Verified(Guid userId, AccountStatus previousStatus, UserRole role, DateTime nowUtc)
    {
        var reinstated = previousStatus == AccountStatus.Suspended;

        // What being verified actually unlocks differs by role, and a generic "you're approved" leaves
        // the person guessing what to do next.
        var whatYouCanDoNow = role switch
        {
            UserRole.Volunteer => "You can now claim listings and collect food.",
            UserRole.Recipient => "You can now receive food donations.",
            _ => "Your account is now fully active.",
        };

        return new Notification
        {
            UserId = userId,
            Type = "AccountVerified",
            Title = reinstated ? "Your account has been reinstated" : "Your account has been verified",
            Body = reinstated
                ? $"An admin has lifted the suspension on your account. {whatYouCanDoNow}"
                : $"An admin has reviewed and approved your account. {whatYouCanDoNow}",
            IsRead = false,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
