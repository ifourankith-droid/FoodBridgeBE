using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Api.Common;

/// <summary>
/// Creates the platform's first Admin account from configuration, once.
/// <para>
/// Needed because the seeded users live in a <c>[Profile("Development")]</c> migration and
/// <c>RegisterRequestValidator</c> won't let anyone self-register as an Admin — so a freshly
/// provisioned database (e.g. Azure SQL) has <b>no admin at all</b>. With nobody in that role there
/// is no way to verify a volunteer, resolve a dispute, or manage drop-off locations: the platform is
/// unusable rather than merely incomplete.
/// </para>
/// <para>
/// Idempotent and opt-in: does nothing unless <c>Bootstrap:AdminMobile</c> is configured, and nothing
/// again once an account with that mobile exists. Safe to leave enabled across restarts and safe to
/// run on every instance of a scaled-out deployment.
/// </para>
/// </summary>
public static class AdminBootstrapper
{
    public const string MobileKey = "Bootstrap:AdminMobile";
    public const string NameKey = "Bootstrap:AdminName";

    public static async Task EnsureAdminAsync(IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        var mobile = configuration[MobileKey];
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return;
        }

        mobile = mobile.Trim();

        using var scope = services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var existing = await userRepository.GetByMobileAsync(mobile, cancellationToken);
        if (existing is not null)
        {
            // Deliberately does not *promote* an existing account. Quietly turning whatever account
            // happens to own this number into an admin would be a privilege-escalation footgun if the
            // setting were ever mistyped.
            if (existing.Role != UserRole.Admin)
            {
                logger.LogWarning(
                    "{Key} is set to {Mobile}, but that number already belongs to a {Role} account. No admin was created and the existing account was left untouched.",
                    MobileKey,
                    mobile,
                    existing.Role);
            }

            return;
        }

        var now = clock.UtcNow;
        var admin = new User
        {
            Mobile = mobile,
            Name = configuration[NameKey]?.Trim() is { Length: > 0 } name ? name : "FoodBridge Admin",
            Role = UserRole.Admin,
            IsAvailable = true,
            AccountStatus = AccountStatus.Verified,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        admin.Id = await userRepository.CreateAsync(admin, cancellationToken: cancellationToken);

        // Logged at Information so the very first deployment leaves evidence of who was granted
        // admin, and when — this is the one account nobody reviewed.
        logger.LogInformation(
            "Bootstrapped the initial Admin account {AdminId} for mobile {Mobile}. Sign in with the normal OTP flow.",
            admin.Id,
            mobile);
    }
}
