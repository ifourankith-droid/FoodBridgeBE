using FluentValidation;
using FoodBridge.Application.Auth.Dtos;

namespace FoodBridge.Application.Auth.Validators;

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty()
            .Matches(@"^[6-9]\d{9}$")
            .WithMessage("Mobile must be a valid 10-digit Indian mobile number.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Code must be a 6-digit OTP.");
    }
}
