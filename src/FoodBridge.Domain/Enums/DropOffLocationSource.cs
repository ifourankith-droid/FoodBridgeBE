namespace FoodBridge.Domain.Enums;

/// <summary>Where a drop-off location came from. Stored as tinyint.</summary>
public enum DropOffLocationSource : byte
{
    /// <summary>Curated by an admin — a partner NGO office, shelter, or community fridge.</summary>
    Admin = 1,

    /// <summary>
    /// Discovered in the field and saved automatically when a delivery was recorded there.
    /// Live immediately (an admin can deactivate it), so the pool grows from real use.
    /// <para>
    /// Named for the volunteer case it was built for, but it now also covers a donor delivering
    /// their own unclaimed listing — read it as "added during a delivery". The name is kept
    /// because the value is persisted and on the wire; <c>AddedByUserId</c> is who actually
    /// added it, and that is what the UI shows.
    /// </para>
    /// </summary>
    Volunteer = 2,
}
