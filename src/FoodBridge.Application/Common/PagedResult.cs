namespace FoodBridge.Application.Common;

/// <summary>
/// A page of service-layer data plus the paging metadata needed to build a
/// <see cref="PagedResponse{T}"/>, threaded through <see cref="Result{T}"/>.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
