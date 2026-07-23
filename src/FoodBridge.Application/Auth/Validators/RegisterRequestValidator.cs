using FluentValidation;
using FoodBridge.Application.Auth.Dtos;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Auth.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly string[] RegistrableRoles =
    {
        nameof(UserRole.Donor),
        nameof(UserRole.Volunteer),
        nameof(UserRole.Recipient),
    };

    public RegisterRequestValidator()
    {
        RuleFor(x => x.SessionToken).NotEmpty();

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => RegistrableRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Role must be one of: {string.Join(", ", RegistrableRoles)}.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(500);

        RuleFor(x => x.CapacityMeals)
            .NotNull()
            .GreaterThan(0)
            .When(x => string.Equals(x.Role, nameof(UserRole.Recipient), StringComparison.OrdinalIgnoreCase))
            .WithMessage("CapacityMeals is required for recipients.");

        RuleFor(x => x.RecipientType)
            .NotEmpty()
            .Must(type => type is not null &&
                (string.Equals(type, nameof(RecipientType.Individual), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, nameof(RecipientType.Organization), StringComparison.OrdinalIgnoreCase)))
            .When(x => string.Equals(x.Role, nameof(UserRole.Recipient), StringComparison.OrdinalIgnoreCase))
            .WithMessage("RecipientType must be 'Individual' or 'Organization' for recipients.");

        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}
