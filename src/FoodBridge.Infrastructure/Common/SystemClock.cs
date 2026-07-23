using FoodBridge.Application.Abstractions;

namespace FoodBridge.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
