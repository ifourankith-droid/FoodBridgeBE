using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IOtpCodeRepository
{
    Task<int> CountSentSinceAsync(string mobile, DateTime sinceUtc, CancellationToken cancellationToken = default);

    Task CreateAsync(OtpCode otpCode, CancellationToken cancellationToken = default);

    Task<OtpCode?> GetLatestActiveAsync(string mobile, CancellationToken cancellationToken = default);

    Task IncrementAttemptsAsync(Guid id, CancellationToken cancellationToken = default);

    Task ConsumeAsync(Guid id, CancellationToken cancellationToken = default);
}
