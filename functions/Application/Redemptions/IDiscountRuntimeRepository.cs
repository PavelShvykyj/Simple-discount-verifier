using SimpleDiscountVerifier.Api.Application.Common;

namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public interface IDiscountRuntimeRepository
{
    Task<DiscountRuntimeRecord?> GetCurrentAsync(
        string phoneRuntimeKey,
        CancellationToken cancellationToken);

    Task<DiscountRuntimeRecord?> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a brand-new current runtime row. Fails with <see cref="StorageWriteStatus.Conflict"/>
    /// if a row already exists for the phone runtime key, so callers can safely use this as the
    /// "no existing row yet" branch of an optimistic-concurrency reserve loop.
    /// </summary>
    Task<StorageWriteResult> InsertCurrentAsync(
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
