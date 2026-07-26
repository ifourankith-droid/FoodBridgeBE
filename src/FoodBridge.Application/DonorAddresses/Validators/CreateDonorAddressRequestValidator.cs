using FluentValidation;
using FoodBridge.Application.DonorAddresses.Dtos;

namespace FoodBridge.Application.DonorAddresses.Validators;

public sealed class CreateDonorAddressRequestValidator : AbstractValidator<CreateDonorAddressRequest>
{
    public CreateDonorAddressRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}
