using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Users.Dtos;

namespace FoodBridge.Application.Users.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(PostalRules.CityMaxLength);
        RuleFor(x => x.State).MaximumLength(PostalRules.StateMaxLength);
        RuleFor(x => x.Pincode).ValidPincode();
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.CapacityMeals).GreaterThan(0).When(x => x.CapacityMeals.HasValue);
    }
}
