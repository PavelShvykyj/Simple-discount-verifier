namespace SimpleDiscountVerifier.Api.Domain.Shared;

public sealed class DomainValidationResult<T>
{
    private DomainValidationResult(T? value, IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    public T? Value { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static DomainValidationResult<T> Success(T value) => new(value, []);

    public static DomainValidationResult<T> Failure(params string[] errors) => new(default, errors);
}
