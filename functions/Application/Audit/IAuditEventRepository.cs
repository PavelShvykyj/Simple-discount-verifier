using SimpleDiscountVerifier.Api.Application.Common;

namespace SimpleDiscountVerifier.Api.Application.Audit;

public interface IAuditEventRepository
{
    Task<StorageWriteResult> InsertAsync(
        AuditEventRecord auditEvent,
        CancellationToken cancellationToken);

    Task<PagedResult<AuditEventRecord>> ListByCorrelationIdAsync(
        string correlationId,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken);
}
