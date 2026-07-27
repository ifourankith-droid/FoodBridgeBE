namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection — one donor's share of a recipient's total confirmed meals received.</summary>
public sealed class DonorMealShare
{
    public Guid DonorId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public int TotalMealsReceived { get; set; }
}
