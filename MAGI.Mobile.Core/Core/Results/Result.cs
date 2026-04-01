namespace MAGI.Mobile.Core.Core.Results;

public class Result
{
    public bool IsSuccess { get; }
    public string ErrorMessage { get; }
    public bool IsFromCache { get; }

    protected Result(bool isSuccess, string errorMessage, bool isFromCache)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        IsFromCache = isFromCache;
    }

    public static Result Success(bool isFromCache = false) => new(true, string.Empty, isFromCache);

    public static Result Failure(string errorMessage) => new(false, errorMessage, false);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string errorMessage, bool isFromCache)
        : base(isSuccess, errorMessage, isFromCache)
    {
        Value = value;
    }

    public static Result<T> Success(T value, bool isFromCache = false) => new(true, value, string.Empty, isFromCache);

    public static new Result<T> Failure(string errorMessage) => new(false, default, errorMessage, false);
}