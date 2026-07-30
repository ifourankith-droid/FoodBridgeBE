namespace FoodBridge.Application.Common;

public sealed class FeatureSettings
{
    public const string SectionName = "Features";

    /// <summary>
    /// Whether the Recipient role is offered as part of the product. When false the
    /// platform runs on three roles (Donor, Volunteer, Admin): new Recipient
    /// registrations are refused, and a volunteer's confirm-pickup no longer
    /// auto-matches a recipient — so confirm-delivery completes the donation outright
    /// (points + certificate) instead of waiting on a recipient's confirm-receipt.
    /// <para>
    /// Deliberately NOT a kill switch on the recipient endpoints: accounts that already
    /// exist keep working, so an existing recipient can still sign in, proactively
    /// request a listing, and run the full legacy accept/reject/confirm-receipt flow.
    /// Behaviour keys off whether a listing actually has a RecipientId, not off this
    /// flag directly, so both paths stay correct at the same time.
    /// </para>
    /// </summary>
    public bool RecipientRoleEnabled { get; set; }
}
