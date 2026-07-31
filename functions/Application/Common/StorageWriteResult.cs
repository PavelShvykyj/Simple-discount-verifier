namespace SimpleDiscountVerifier.Api.Application.Common;

public sealed record StorageWriteResult(StorageWriteStatus Status)
{
    public bool Succeeded => Status is StorageWriteStatus.Created or StorageWriteStatus.Updated;

    public static StorageWriteResult Created { get; } = new(StorageWriteStatus.Created);

    public static StorageWriteResult Updated { get; } = new(StorageWriteStatus.Updated);

    public static StorageWriteResult Conflict { get; } = new(StorageWriteStatus.Conflict);

    public static StorageWriteResult NotFound { get; } = new(StorageWriteStatus.NotFound);

    public static StorageWriteResult PreconditionFailed { get; } = new(StorageWriteStatus.PreconditionFailed);
}

public enum StorageWriteStatus
{
    Created,
    Updated,
    Conflict,
    NotFound,
    PreconditionFailed
}
