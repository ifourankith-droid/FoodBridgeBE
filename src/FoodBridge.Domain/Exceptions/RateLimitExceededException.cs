namespace FoodBridge.Domain.Exceptions;

/// <summary>
/// Distinct from <see cref="BusinessRuleException"/> (422) — maps to 429 Too Many Requests.
/// </summary>
public sealed class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message)
    {
    }
}
