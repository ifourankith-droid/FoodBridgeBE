namespace FoodBridge.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
