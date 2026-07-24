using FluentValidation;
using FoodBridge.Application.Disputes.Dtos;

namespace FoodBridge.Application.Disputes.Validators;

public sealed class ResolveDisputeRequestValidator : AbstractValidator<ResolveDisputeRequest>
{
    public ResolveDisputeRequestValidator()
    {
        RuleFor(x => x.ResolutionNote).NotEmpty().MaximumLength(1000);
    }
}
