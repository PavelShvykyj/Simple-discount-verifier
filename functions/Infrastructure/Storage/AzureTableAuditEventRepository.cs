using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

public sealed class AzureTableAuditEventRepository : IAuditEventRepository
{
    private readonly TableClient _tableClient;

    public AzureTableAuditEventRepository(TableClientProvider tableClientProvider)
    {
        _tableClient = tableClientProvider.CreateAuditEventsClient();
    }

    public async Task<StorageWriteResult> InsertAsync(
        AuditEventRecord auditEvent,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < StorageConstants.AuditInsertMaxAttempts; attempt++)
        {
            try
            {
                await _tableClient.AddEntityAsync(ToEntity(auditEvent), cancellationToken);
                return StorageWriteResult.Created;
            }
            catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsConflict(exception))
            {
                if (attempt == StorageConstants.AuditInsertMaxAttempts - 1)
                {
                    return StorageWriteResult.Conflict;
                }
            }
        }

        return StorageWriteResult.Conflict;
    }

    public async Task<PagedResult<AuditEventRecord>> ListByCorrelationIdAsync(
        string correlationId,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var query = _tableClient
            .QueryAsync<TableEntity>(
                entity => entity.PartitionKey == correlationId,
                maxPerPage: pageSize,
                cancellationToken: cancellationToken)
            .AsPages(continuationToken, pageSize);

        await foreach (var page in query.WithCancellation(cancellationToken))
        {
            return new PagedResult<AuditEventRecord>(
                page.Values.Select(ToRecord).ToArray(),
                page.ContinuationToken);
        }

        return new PagedResult<AuditEventRecord>([], null);
    }

    private static TableEntity ToEntity(AuditEventRecord auditEvent)
    {
        var entity = new TableEntity(auditEvent.CorrelationId, BuildRowKey(auditEvent.OccurredAtUtc))
        {
            [StorageConstants.Properties.EventType] = auditEvent.EventType,
            [StorageConstants.Properties.ActorType] = auditEvent.ActorType,
            [StorageConstants.Properties.OccurredAtUtc] = auditEvent.OccurredAtUtc,
            [StorageConstants.Properties.MetadataJson] = JsonSerializer.Serialize(auditEvent.Metadata, StorageJson.Options)
        };

        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.PhoneHash, auditEvent.PhoneHash);
        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.ActorId, auditEvent.ActorId);

        return entity;
    }

    private static AuditEventRecord ToRecord(TableEntity entity)
    {
        var metadataJson = TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.MetadataJson);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            metadataJson,
            StorageJson.Options) ?? new Dictionary<string, string?>();

        return new AuditEventRecord(
            entity.PartitionKey,
            TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.EventType),
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.OccurredAtUtc),
            TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.ActorType),
            TableStorageMapper.GetOptionalString(entity, StorageConstants.Properties.ActorId),
            TableStorageMapper.GetOptionalString(entity, StorageConstants.Properties.PhoneHash),
            metadata);
    }

    private static string BuildRowKey(DateTimeOffset occurredAtUtc)
    {
        var suffix = RandomNumberGenerator
            .GetInt32(AuditRowKeyRandomSuffixUpperBound)
            .ToString($"D{StorageConstants.AuditRowKeyRandomSuffixLength}");

        return $"{occurredAtUtc.UtcDateTime:yyyyMMddTHHmmssfffZ}_ae_{suffix}";
    }

    private static int AuditRowKeyRandomSuffixUpperBound =>
        (int)Math.Pow(10, StorageConstants.AuditRowKeyRandomSuffixLength);
}
