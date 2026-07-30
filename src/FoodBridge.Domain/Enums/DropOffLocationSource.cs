namespace FoodBridge.Domain.Enums;

/// <summary>Where a drop-off location came from. Stored as tinyint.</summary>
public enum DropOffLocationSource : byte
{
    /// <summary>Curated by an admin — a partner NGO office, shelter, or community fridge.</summary>
    Admin = 1,

    /// <summary>
    /// Discovered in the field and saved automatically when a volunteer recorded a delivery
    /// there. Live immediately (an admin can deactivate it), so the pool grows from real use.
    /// </summary>
    Volunteer = 2,
}
