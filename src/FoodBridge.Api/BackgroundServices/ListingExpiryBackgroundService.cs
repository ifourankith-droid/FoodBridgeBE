using FoodBridge.Application.Abstractions;

namespace FoodBridge.Api.BackgroundServices;

/// <summary>
/// Periodically flips Pending listings whose pickup deadline has passed to Expired.
/// Runs immediately on startup (not after waiting a full interval first), then every
/// 30 seconds — comfortably within the "within a minute of startup" requirement.
/// </summary>
public sealed class ListingExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ListingExpiryBackgroundService> _logger;

    public ListingExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ListingExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await ExpireOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpireOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var listingRepository = scope.ServiceProvider.GetRequiredService<IListingRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        try
        {
            var expiredIds = await listingRepository.ExpirePastDeadlineListingsAsync(clock.UtcNow, cancellationToken);
            if (expiredIds.Count > 0)
            {
                _logger.LogInformation("Listing expiry sweep flipped {Count} listing(s) to Expired.", expiredIds.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Listing expiry sweep failed.");
        }
    }
}
