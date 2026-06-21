using SimpleDiscountVerifier.Api.Application.Common;

namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public interface IDiscountRuntimeRepository
{
    Task<DiscountRuntimeRecord?> GetCurrentAsync(
        string phoneRuntimeKey,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> UpsertCurrentAsync(
        DiscountRuntimeRecord record,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> ReplaceCurrentAsync(
        DiscountRuntimeRecord record,
        string concurrencyToken,
        CancellationToken cancellationToken);

    Task<PagedResult<DiscountRuntimeRecord>> ListUpdatedBeforeAsync(
        DateTimeOffset updatedBeforeUtc,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> DeleteCurrentAsync(
        string phoneRuntimeKey,
        string concurrencyToken,
        CancellationToken cancellationToken);
}
