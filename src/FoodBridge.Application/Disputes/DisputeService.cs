using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Disputes.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Disputes;

public sealed class DisputeService : IDisputeService
{
    private readonly IDisputeRepository _disputeRepository;
    private readonly IListingRepository _listingRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DisputeService(IDisputeRepository disputeRepository, IListingRepository listingRepository, ICurrentUser currentUser, IClock clock)
    {
        _disputeRepository = disputeRepository;
        _listingRepository = listingRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<DisputeResponse>> CreateAsync(CreateDisputeRequest request, CancellationToken cancellationToken = default)
    {
        var listing = await _listingRepository.GetByIdAsync(request.ListingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", request.ListingId);
        }

        var userId = _currentUser.UserId;
        if (listing.DonorId != userId && listing.VolunteerId != userId && listing.RecipientId != userId)
        {
            throw new UnauthorizedAccessException("You can only raise a dispute for a listing you're involved in.");
        }

        var now = _clock.UtcNow;
        var dispute = new Dispute
        {
            ListingId = request.ListingId,
            RaisedByUserId = userId,
            Reason = request.Reason,
            Status = DisputeStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dispute.Id = await _disputeRepository.CreateAsync(dispute, cancellationToken);

        return Result.Success(dispute.ToResponse(), "Dispute raised successfully.");
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
