namespace FoodBridge.Domain.Enums;

/// <summary>
/// Only meaningful when <see cref="UserRole"/> is Recipient.
/// </summary>
public enum RecipientType : byte
{
    Individual = 1,
    Organization = 2,
}
