namespace FoodBridge.Application.Auth.Dtos;

public sealed record VerifyOtpRequest(string Mobile, string Code);
