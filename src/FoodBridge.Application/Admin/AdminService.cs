using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Admin.Dtos;
using FoodBridge.Application.Common;
using FoodBridge.Application.Users;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Admin;

public sealed class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserDocumentRepository _userDocumentRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AdminService(
        IAdminRepository adminRepository,
        IUserRepository userRepository,
        IUserDocumentRepository userDocumentRepository,
        INotificationDispatcher notificationDispatcher,
        ICurrentUser currentUser,
        IClock clock)
    {
        _adminRepository = adminRepository;
        _userRepository = userRepository;
        _userDocumentRepository = userDocumentRepository;
        _notificationDispatcher = notificationDispatcher;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<AdminDashboardResponse>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _adminRepository.GetDashboardStatsAsync(cancellationToken);
        var listingsByStatus = await _adminRepository.GetListingsByStatusAsync(cancellationToken);
        var accountsByStatus = await _adminRepository.GetAccountsByStatusAsync(cancellationToken);
        return Result.Success(stats.ToResponse(listingsByStatus, accountsByStatus));
    }

    public async Task<Result<PagedResult<AdminListingSummaryResponse>>> GetAllListingsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ListingStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ListingStatus>(status, true, out var parsed))
            {
                return Result.Failure<PagedResult<AdminListingSummaryResponse>>($"Unknown status '{status}'.");
            }

            statusFilter = parsed;
        }

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _adminRepository.GetAllListingsAsync(statusFilter, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<AdminListingSummaryResponse>(items.Select(i => i.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<PagedResult<AdminUserSummaryResponse>>> GetAllUsersAsync(string? role, string? accountStatus, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        UserRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role, true, out var parsedRole))
            {
                return Result.Failure<PagedResult<AdminUserSummaryResponse>>($"Unknown role '{role}'.");
            }

            roleFilter = parsedRole;
        }

        AccountStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(accountStatus))
        {
            if (!Enum.TryParse<AccountStatus>(accountStatus, true, out var parsedStatus))
            {
                return Result.Failure<PagedResult<AdminUserSummaryResponse>>($"Unknown account status '{accountStatus}'.");
            }

            statusFilter = parsedStatus;
        }

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _adminRepository.GetAllUsersAsync(roleFilter, statusFilter, normalizedPage, normalizedPageSize, cancellationToken);

        // One batched lookup for the whole page, only for the rows that actually need documents —
        // filling this per row would be an N+1 on the queue this page exists to clear.
        var needsDocuments = items.Where(i => VerificationPolicy.RequiredDocuments(i.Role).Count > 0).Select(i => i.Id).ToList();
        if (needsDocuments.Count > 0)
        {
            var submitted = await _userDocumentRepository.GetDocumentsForUsersAsync(needsDocuments, cancellationToken);
            foreach (var item in items)
            {
                if (submitted.TryGetValue(item.Id, out var docs))
                {
                    item.SubmittedDocumentTypes = docs.Select(d => d.Type).ToList();
                    item.SelfieUrl = docs.FirstOrDefault(d => d.Type == UserDocumentType.Selfie)?.FileUrl;
                }
            }
        }

        return Result.Success(new PagedResult<AdminUserSummaryResponse>(items.Select(i => i.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<AdminUserSummaryResponse>> VerifyAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        // Verify is deliberately unconditional (it doubles as the only way to reverse a suspension),
        // so re-verifying an already-Verified account is a legitimate no-op — and must not spam them
        // with "you've been verified" every time an admin clicks it. A Suspended → Verified move does
        // notify: being reinstated is exactly the kind of thing you want to hear about.
        var previousStatus = user.AccountStatus;
        var notification = previousStatus == AccountStatus.Verified
            ? null
            : AccountNotifications.Verified(userId, previousStatus, user.Role, _clock.UtcNow);

        await _userRepository.UpdateAccountStatusAsync(userId, AccountStatus.Verified, notification, cancellationToken);
        user.AccountStatus = AccountStatus.Verified;

        // Best-effort live push after the atomic write has committed — the same ordering every other
        // notification in this app uses. GET /api/notifications is the fallback if nobody is connected.
        if (notification is not null)
        {
            await _notificationDispatcher.DispatchAsync(notification, cancellationToken);
        }

        return Result.Success(user.ToResponse());
    }

    public async Task<Result<AdminUserSummaryResponse>> SuspendAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        if (user.Role == UserRole.Admin)
        {
            return Result.Failure<AdminUserSummaryResponse>("Admin accounts cannot be suspended.");
        }

        if (user.Id == _currentUser.UserId)
        {
            return Result.Failure<AdminUserSummaryResponse>("You cannot suspend your own account.");
        }

        // No notification on suspend — only verify was in scope. The plumbing is now here, so adding
        // one is a two-line change if you want suspended users told why their claims stopped working.
        await _userRepository.UpdateAccountStatusAsync(userId, AccountStatus.Suspended, cancellationToken: cancellationToken);
        user.AccountStatus = AccountStatus.Suspended;
        return Result.Success(user.ToResponse());
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User", userId);
        }

        return user;
    }
}
