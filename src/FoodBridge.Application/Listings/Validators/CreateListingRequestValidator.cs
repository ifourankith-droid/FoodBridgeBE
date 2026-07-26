using FluentValidation;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Listings.Validators;

public sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    public CreateListingRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FoodType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DietType).Must(v => v is null || Enum.TryParse<DietType>(v, true, out _))
            .WithMessage("DietType must be one of: Veg, NonVeg.");
        RuleFor(x => x.MealType).Must(v => v is null || Enum.TryParse<MealType>(v, true, out _))
            .WithMessage("MealType must be one of: Breakfast, Lunch, Dinner, Snacks.");
        RuleFor(x => x.QuantityMeals).GreaterThan(0);
        RuleFor(x => x.FreshnessTag).NotEmpty().Must(v => Enum.TryParse<FreshnessTag>(v, true, out _))
            .WithMessage("FreshnessTag must be one of: JustCooked, FewHoursOld, Packaged.");
        RuleFor(x => x.PickupDeadlineUtc).GreaterThan(x => x.PreparedAtUtc ?? DateTime.MinValue)
            .WithMessage("PickupDeadlineUtc must be after PreparedAtUtc.")
            .When(x => x.PreparedAtUtc.HasValue);
        RuleFor(x => x)
            .Must(x => x.DonorAddressId.HasValue || (!string.IsNullOrWhiteSpace(x.PickupAddress) && x.Latitude.HasValue && x.Longitude.HasValue))
            .WithMessage("Provide either donorAddressId or pickupAddress, latitude, and longitude.")
            .WithName("DonorAddressId");
        RuleFor(x => x)
            .Must(x => !(x.DonorAddressId.HasValue && (x.PickupAddress is not null || x.Latitude.HasValue || x.Longitude.HasValue)))
            .WithMessage("Provide either donorAddressId or pickupAddress/latitude/longitude, not both.")
            .WithName("DonorAddressId");

        RuleFor(x => x.PickupAddress).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}
