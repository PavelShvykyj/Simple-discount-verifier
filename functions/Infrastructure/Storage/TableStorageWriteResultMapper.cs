using Azure;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

internal static class TableStorageWriteResultMapper
{
    public const int ConflictStatusCode = 409;
    public const int NotFoundStatusCode = 404;
    public const int PreconditionFailedStatusCode = 412;

    public static bool IsConflict(RequestFailedException exception) =>
        exception.Status == ConflictStatusCode;

    public static bool IsNotFound(RequestFailedException exception) =>
        exception.Status == NotFoundStatusCode;

    public static bool IsPreconditionFailed(RequestFailedException exception) =>
        exception.Status == PreconditionFailedStatusCode;
}
