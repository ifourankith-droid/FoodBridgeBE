namespace FoodBridge.Application.Dashboard.Dtos;

public sealed record DonorMealShareResponse(Guid DonorId, string DonorName, int TotalMealsReceived);
