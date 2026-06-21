using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Domain.Security;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

public sealed class TableAuditWriter : IAuditWriter
{
    private const string AuditPhoneHashSecretSettingName = "AuditPhoneHashSecret";
    private const string CorrelationIdTelemetryTagName = "sdv.correlation_id";

    private readonly IAuditEventRepository _auditEventRepository;
    private readonly HashingOptions _hashingOptions;
    private readonly IClock _clock;
    private readonly ILogger<TableAuditWriter> _logger;

    public TableAuditWriter(
        IAuditEventRepository auditEventRepository,
        IOptions<HashingOptions> hashingOptions,
        IClock clock,
        ILogger<TableAuditWriter> logger)
    {
        _auditEventRepository = auditEventRepository;
        _hashingOptions = hashingOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task WriteAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetTag(CorrelationIdTelemetryTagName, request.CorrelationId);

        var phoneHash = request.Phone is null
            ? null
            : HashPhone(request.Phone.Value);

        var auditEvent = new AuditEventRecord(
            request.CorrelationId,
            request.EventType,
            _clock.UtcNow,
            request.ActorType,
            request.ActorId,
            phoneHash,
            AuditMetadataSanitizer.Sanitize(request.Metadata));

        var result = await _auditEventRepository.InsertAsync(auditEvent, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Audit event write failed with status {Status} for correlation {CorrelationId} and event {EventType}.",
                result.Status,
                request.CorrelationId,
                request.EventType);
        }
    }

    private string HashPhone(string normalizedPhone)
    {
        if (string.IsNullOrWhiteSpace(_hashingOptions.AuditPhoneHashSecret))
        {
            throw new InvalidOperationException(
                $"Application setting '{AuditPhoneHashSecretSettingName}' is required.");
        }

        return SecretHasher.HashPhone(normalizedPhone, _hashingOptions.AuditPhoneHashSecret);
    }
}
