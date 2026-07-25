using FluentValidation;
using FoodBridge.Application.Disputes.Dtos;

namespace FoodBridge.Application.Disputes.Validators;

public sealed class CreateDisputeRequestValidator : AbstractValidator<CreateDisputeRequest>
{
    public CreateDisputeRequestValidator()
    {
        RuleFor(x => x.ListingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
