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
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        try
        {
            var sweep = await listingRepository.ExpirePastDeadlineListingsAsync(clock.UtcNow, cancellationToken);
            if (sweep.RevertedToPendingIds.Count > 0)
            {
                _logger.LogInformation("Listing expiry sweep reverted {Count} abandoned Claimed listing(s) back to Pending.", sweep.RevertedToPendingIds.Count);
            }

            if (sweep.ExpiredIds.Count > 0)
            {
                _logger.LogInformation("Listing expiry sweep flipped {Count} listing(s) to Expired.", sweep.ExpiredIds.Count);
            }

            // The rows are already committed; this is only the live push. Failures are logged
            // and swallowed per notification so one dead connection can't stop the rest — the
            // affected user still sees it via GET /api/notifications either way.
            foreach (var notification in sweep.Notifications)
            {
                try
                {
                    await dispatcher.DispatchAsync(notification, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to push expiry notification {NotificationId} to user {UserId}.", notification.Id, notification.UserId);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Listing expiry sweep failed.");
        }
    }
}
