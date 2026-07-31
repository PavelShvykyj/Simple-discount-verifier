namespace SimpleDiscountVerifier.Api.Application.Common;

public sealed class ApplicationResult<T>
{
    private ApplicationResult(T? value, ApplicationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public ApplicationError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ApplicationResult<T> Success(T value) => new(value, null);

    public static ApplicationResult<T> Failure(ApplicationError error) => new(default, error);
}
