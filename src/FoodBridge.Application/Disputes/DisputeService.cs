using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Disputes.Dtos;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Disputes;

public sealed class DisputeService : IDisputeService
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly ICurrentUser _currentUser;

    public DisputeService(IDisputeRepository disputeRepository, ICurrentUser currentUser)
    {
        _disputeRepository = disputeRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<DisputeResponse>>> GetAllAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        DisputeStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DisputeStatus>(status, true, out var parsed))
            {
                return Result.Failure<PagedResult<DisputeResponse>>($"Unknown status '{status}'.");
            }

            statusFilter = parsed;
        }

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _disputeRepository.GetAllAsync(statusFilter, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<DisputeResponse>(items.Select(d => d.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<DisputeResponse>> ResolveAsync(Guid disputeId, ResolveDisputeRequest request, CancellationToken cancellationToken = default)
    {
        var dispute = await _disputeRepository.GetByIdAsync(disputeId, cancellationToken);
        if (dispute is null)
        {
            throw new NotFoundException("Dispute", disputeId);
        }

        if (dispute.Status == DisputeStatus.Resolved)
        {
            return Result.Failure<DisputeResponse>("This dispute has already been resolved.");
        }

        await _disputeRepository.ResolveAsync(disputeId, _currentUser.UserId, request.ResolutionNote, cancellationToken);

        dispute.Status = DisputeStatus.Resolved;
        dispute.ResolvedByUserId = _currentUser.UserId;
        dispute.ResolutionNote = request.ResolutionNote;

        return Result.Success(dispute.ToResponse());
    }
}
