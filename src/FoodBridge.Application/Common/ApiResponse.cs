namespace FoodBridge.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public string TraceId { get; init; } = string.Empty;

    public static ApiResponse<T> Ok(T data, string message = "Success", string traceId = "") => new()
    {
        Success = true,
        Message = message,
        Data = data,
        TraceId = traceId,
    };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null, string traceId = "") => new()
    {
        Success = false,
        Message = message,
        Errors = errors,
        TraceId = traceId,
    };
}
