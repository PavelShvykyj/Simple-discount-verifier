namespace SimpleDiscountVerifier.Api.Application.Audit;

public interface IAuditWriter
{
    Task WriteAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken);
}
