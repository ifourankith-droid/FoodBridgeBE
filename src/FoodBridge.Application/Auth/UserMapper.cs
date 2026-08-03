using FoodBridge.Application.Auth.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Auth;

public static class UserMapper
{
    /// <param name="preferredAddress">
    /// A saved address to report instead of the account's own — the donor's default, which is the one
    /// carrying a label. Omit it (or pass null) and the account address from the Users row is used.
    /// </param>
    public static UserResponse ToResponse(this User user, DonorAddress? preferredAddress = null) => new(
        user.Id,
        user.Mobile,
        user.Name,
        user.Role.ToString(),
        user.City,
        user.AccountStatus.ToString(),
        user.RecipientType?.ToString(),
        user.AvatarUrl,
        BuildAddress(user, preferredAddress));

    /// <summary>
    /// Null rather than an all-null block when there is no address at all, so a caller can test the
    /// object itself instead of probing every field.
    /// </summary>
    private static AddressResponse? BuildAddress(User user, DonorAddress? preferred)
    {
        if (preferred is not null)
        {
            return new AddressResponse(
                preferred.Label,
                preferred.Address,
                preferred.City,
                preferred.State,
                preferred.Pincode,
                preferred.Latitude,
                preferred.Longitude);
        }

        var hasAny = !string.IsNullOrWhiteSpace(user.Address)
            || !string.IsNullOrWhiteSpace(user.City)
            || !string.IsNullOrWhiteSpace(user.State)
            || !string.IsNullOrWhiteSpace(user.Pincode);

        return hasAny
            // No label: the Users row has never had one. Only saved addresses are labelled.
            ? new AddressResponse(null, user.Address, user.City, user.State, user.Pincode, user.Latitude, user.Longitude)
            : null;
    }
}
