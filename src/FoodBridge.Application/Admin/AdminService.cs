using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Admin.Dtos;
using FoodBridge.Application.Common;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Admin;

public sealed class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    public AdminService(IAdminRepository adminRepository, IUserRepository userRepository, ICurrentUser currentUser)
    {
        _adminRepository = adminRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminDashboardResponse>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _adminRepository.GetDashboardStatsAsync(cancellationToken);
        return Result.Success(stats.ToResponse());
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
        return Result.Success(new PagedResult<AdminUserSummaryResponse>(items.Select(i => i.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<AdminUserSummaryResponse>> VerifyAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        await _userRepository.UpdateAccountStatusAsync(userId, AccountStatus.Verified, cancellationToken);
        user.AccountStatus = AccountStatus.Verified;
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

        await _userRepository.UpdateAccountStatusAsync(userId, AccountStatus.Suspended, cancellationToken);
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
