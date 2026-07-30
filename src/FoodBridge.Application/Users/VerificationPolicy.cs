using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Users;

/// <summary>
/// Who has to prove who they are, and with what. Single source of truth — the registration path
/// (which initial <see cref="AccountStatus"/> to assign), the volunteer's verification screen, the
/// admin's review queue, and the claim-time enforcement all read it, so they cannot drift apart.
/// </summary>
public static class VerificationPolicy
{
    /// <summary>
    /// A government photo ID plus a selfie to check it against. Verifying the ID alone only proves
    /// someone owns an ID, not that they're the person holding it.
    /// </summary>
    private static readonly UserDocumentType[] VolunteerDocuments =
    {
        UserDocumentType.IdProof,
        UserDocumentType.Selfie,
    };

    private static readonly UserDocumentType[] NoDocuments = Array.Empty<UserDocumentType>();

    /// <summary>
    /// Whether this role must be reviewed by an admin before it can act.
    /// <para>
    /// Volunteers only, deliberately. A volunteer takes physical custody of a stranger's food and
    /// travels with it unsupervised — that is the trust-critical role. A donor gives food away and a
    /// fake one mostly wastes a volunteer's trip; gating them too would block genuine surplus from
    /// being posted while it spoils. Admins are seeded, never self-registered.
    /// </para>
    /// </summary>
    public static bool RequiresVerification(UserRole role) => role == UserRole.Volunteer;

    /// <summary>The status a newly registered account of this role starts in.</summary>
    public static AccountStatus InitialAccountStatus(UserRole role) =>
        RequiresVerification(role) || role == UserRole.Recipient
            ? AccountStatus.Pending
            : AccountStatus.Verified;

    /// <summary>Documents this role must upload before an admin can review them.</summary>
    public static IReadOnlyList<UserDocumentType> RequiredDocuments(UserRole role) =>
        RequiresVerification(role) ? VolunteerDocuments : NoDocuments;

    /// <summary>
    /// Whether a user in this state may take on <em>new</em> work — claiming a listing, or
    /// collecting food they've claimed.
    /// <para>
    /// Note this deliberately does <b>not</b> gate finishing or releasing work already in hand
    /// (confirm-delivery, unclaim). A volunteer suspended while carrying food must still be able to
    /// record where it went, or the food is stranded with no audit trail — and releasing a claim is
    /// the outcome we want, not one to block. See VolunteerListingService.
    /// </para>
    /// </summary>
    public static bool CanTakeOnWork(UserRole role, AccountStatus status) =>
        !RequiresVerification(role) || status == AccountStatus.Verified;

    /// <summary>
    /// Why they can't, phrased for the person who is blocked. Distinguishes "we haven't got to you
    /// yet" from "we've reviewed you and said no", because those need different actions from them.
    /// </summary>
    public static string BlockedReason(AccountStatus status) => status switch
    {
        AccountStatus.Pending => "Your account is still being verified. Upload your ID and selfie, and an admin will review it shortly.",
        AccountStatus.Suspended => "Your account has been suspended. Please contact support.",
        _ => "Your account is not currently able to take on deliveries.",
    };
}
