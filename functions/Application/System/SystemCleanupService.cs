using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Application.System;

public sealed class SystemCleanupService
{
    private const int CleanupPageSize = 100;
    private const string DiscountRuntimeRetentionHoursSettingName = "DiscountRuntimeRetentionHours";
    private const string AuditEventsRetentionDaysSettingName = "AuditEventsRetentionDays";

    private readonly IDiscountRuntimeRepository _runtimeRepository;
    private readonly IAuditEventRepository _auditEventRepository;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly IClock _clock;
    private readonly ILogger<SystemCleanupService> _logger;

    public SystemCleanupService(
        IDiscountRuntimeRepository runtimeRepository,
        IAuditEventRepository auditEventRepository,
        IOptions<RuntimeOptions> runtimeOptions,
        IClock clock,
        ILogger<SystemCleanupService> logger)
    {
        _runtimeRepository = runtimeRepository;
        _auditEventRepository = auditEventRepository;
        _runtimeOptions = runtimeOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ApplicationResult<SystemCleanupResult>> RunAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (!TryGetPositive(
            _runtimeOptions.DiscountRuntimeRetentionHours,
            DiscountRuntimeRetentionHoursSettingName,
            out var runtimeRetentionHours,
            out var runtimeRetentionError))
        {
            return ApplicationResult<SystemCleanupResult>.Failure(runtimeRetentionError!);
        }

        if (!TryGetPositive(
            _runtimeOptions.AuditEventsRetentionDays,
            AuditEventsRetentionDaysSettingName,
            out var auditRetentionDays,
            out var auditRetentionError))
        {
            return ApplicationResult<SystemCleanupResult>.Failure(auditRetentionError!);
        }

        var runtimeCutoff = now.AddHours(-runtimeRetentionHours);
        var auditCutoff = now.AddDays(-auditRetentionDays);

        var runtime = await CleanupRuntimeAsync(runtimeCutoff, cancellationToken);
        var audit = await CleanupAuditEventsAsync(auditCutoff, cancellationToken);

        _logger.LogInformation(
            "System cleanup completed. Runtime deleted {RuntimeDeleted}/{RuntimeScanned}. Audit deleted {AuditDeleted}/{AuditScanned}.",
            runtime.Deleted,
            runtime.Scanned,
            audit.Deleted,
            audit.Scanned);

        return ApplicationResult<SystemCleanupResult>.Success(
            new SystemCleanupResult(now, runtime, audit));
    }

    private async Task<CleanupTableResult> CleanupRuntimeAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var page = await _runtimeRepository.ListUpdatedBeforeAsync(
            cutoffUtc,
            CleanupPageSize,
            continuationToken: null,
            cancellationToken);
        var deleted = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var item in page.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ConcurrencyToken))
            {
                skipped++;
                continue;
            }

            var result = await _runtimeRepository.DeleteCurrentAsync(
                item.PhoneRuntimeKey,
                item.ConcurrencyToken,
                cancellationToken);

            if (result.Status is StorageWriteStatus.Updated or StorageWriteStatus.NotFound)
            {
                deleted++;
                continue;
            }

            if (result.Status == StorageWriteStatus.PreconditionFailed)
            {
                skipped++;
                continue;
            }

            failed++;
        }

        return new CleanupTableResult(cutoffUtc, page.Items.Count, deleted, skipped, failed);
    }

    private async Task<CleanupTableResult> CleanupAuditEventsAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var page = await _auditEventRepository.ListOccurredBeforeAsync(
            cutoffUtc,
            CleanupPageSize,
            continuationToken: null,
            cancellationToken);
        var deleted = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var item in page.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                skipped++;
                continue;
            }

            var result = await _auditEventRepository.DeleteAsync(
                item.CorrelationId,
                item.Id,
                cancellationToken);

            if (result.Status is StorageWriteStatus.Updated or StorageWriteStatus.NotFound)
            {
                deleted++;
                continue;
            }

            failed++;
        }

        return new CleanupTableResult(cutoffUtc, page.Items.Count, deleted, skipped, failed);
    }

    private static bool TryGetPositive(
        int value,
        string settingName,
        out int positiveValue,
        out ApplicationError? error)
    {
        positiveValue = value;
        error = null;

        if (value > 0)
        {
            return true;
        }

        error = InvalidConfiguration(settingName);
        return false;
    }

    public static ApplicationError Unauthorized() =>
        new(
            SystemErrorCodes.Unauthorized,
            "Cleanup automation key is missing or invalid.",
            HttpStatusCode.Unauthorized);

    private static ApplicationError InvalidConfiguration(string settingName) =>
        new(
            SystemErrorCodes.InvalidConfiguration,
            $"Application setting '{settingName}' must be a positive integer.",
            HttpStatusCode.InternalServerError);
}
