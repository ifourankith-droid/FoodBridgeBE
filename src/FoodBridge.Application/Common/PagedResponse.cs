namespace FoodBridge.Application.Common;

public class PagedResponse<T> : ApiResponse<IReadOnlyList<T>>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }

    public static PagedResponse<T> Create(
        IReadOnlyList<T> data,
        int page,
        int pageSize,
        int totalCount,
        string message = "Success",
        string traceId = "") => new()
    {
        Success = true,
        Message = message,
        Data = data,
        TraceId = traceId,
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
    };

    public static new PagedResponse<T> Fail(string message, IReadOnlyList<string>? errors = null, string traceId = "") => new()
    {
        Success = false,
        Message = message,
        Data = Array.Empty<T>(),
        Errors = errors,
        TraceId = traceId,
    };
}
