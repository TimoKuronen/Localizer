namespace Localizer.Application.UseCases;

public sealed class UseCaseResult
{
    private UseCaseResult(bool succeeded, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static UseCaseResult Success() => new(true, null, null);

    public static UseCaseResult Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);
}

public sealed class UseCaseResult<T>
{
    private UseCaseResult(bool succeeded, T? value, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static UseCaseResult<T> Success(T value) => new(true, value, null, null);

    public static UseCaseResult<T> Failure(string errorCode, string errorMessage) =>
        new(false, default, errorCode, errorMessage);
}
