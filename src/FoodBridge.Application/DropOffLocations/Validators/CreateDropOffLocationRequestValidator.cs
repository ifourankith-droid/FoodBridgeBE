using FluentValidation;
using FoodBridge.Application.DropOffLocations.Dtos;

namespace FoodBridge.Application.DropOffLocations.Validators;

public sealed class CreateDropOffLocationRequestValidator : AbstractValidator<CreateDropOffLocationRequest>
{
    public CreateDropOffLocationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.City).MaximumLength(100);
    }
}
