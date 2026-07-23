using FluentValidation;
using FoodBridge.Application.Auth.Dtos;

namespace FoodBridge.Application.Auth.Validators;

public sealed class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
{
    public SendOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty()
            .Matches(@"^[6-9]\d{9}$")
            .WithMessage("Mobile must be a valid 10-digit Indian mobile number.");
    }
}
