namespace FoodBridge.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public string Message { get; }
    public IReadOnlyList<string>? Errors { get; }

    protected Result(bool isSuccess, string message, IReadOnlyList<string>? errors)
    {
        IsSuccess = isSuccess;
        Message = message;
        Errors = errors;
    }

    public static Result Success(string message = "Success") => new(true, message, null);

    public static Result Failure(string message, IReadOnlyList<string>? errors = null) => new(false, message, errors);

    public static Result<T> Success<T>(T data, string message = "Success") => new(true, message, data, null);

    public static Result<T> Failure<T>(string message, IReadOnlyList<string>? errors = null) => new(false, message, default, errors);
}

public sealed class Result<T> : Result
{
    public T? Data { get; }

    internal Result(bool isSuccess, string message, T? data, IReadOnlyList<string>? errors)
        : base(isSuccess, message, errors)
    {
        Data = data;
    }
}
