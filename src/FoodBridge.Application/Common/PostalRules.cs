using System.Text.RegularExpressions;
using FluentValidation;

namespace FoodBridge.Application.Common;

/// <summary>
/// The address rules, in one place, because four requests carry an address: registration, profile
/// update, and donor-address create/update. Drifting limits between them was the exact failure mode
/// <c>VerificationPolicy</c> was created to stop, so these are shared rather than retyped.
/// <para>
/// Lengths mirror the columns added by <c>M202608031000_AddPostalFieldsToAddresses</c> — City/State
/// <c>nvarchar(100)</c>, Pincode <c>nvarchar(10)</c>. Change them together.
/// </para>
/// </summary>
public static class PostalRules
{
    public const int CityMaxLength = 100;
    public const int StateMaxLength = 100;

    /// <summary>
    /// Six digits. Deliberately not stricter: the platform is India-first, but rejecting anything else
    /// would make the field unusable the day it isn't, and a pincode is display-only — every distance
    /// query runs off the coordinates.
    /// </summary>
    private const string PincodePattern = @"^\d{6}$";

    /// <summary>
    /// Optional everywhere. Blank passes, since the field is genuinely optional and the services
    /// normalise blank to null before storing; a non-blank value must be a well-formed pincode.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ValidPincode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value, PincodePattern))
            .WithMessage("Pincode must be 6 digits.");
}
