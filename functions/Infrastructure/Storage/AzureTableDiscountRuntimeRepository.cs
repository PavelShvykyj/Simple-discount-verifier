using Azure;
using Azure.Data.Tables;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

public sealed class AzureTableDiscountRuntimeRepository : IDiscountRuntimeRepository
{
    private readonly TableClient _tableClient;

    public AzureTableDiscountRuntimeRepository(TableClientProvider tableClientProvider)
    {
        _tableClient = tableClientProvider.CreateDiscountRuntimeClient();
    }

    public async Task<DiscountRuntimeRecord?> GetCurrentAsync(
        string phoneRuntimeKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                BuildPartitionKey(phoneRuntimeKey),
                StorageConstants.CurrentRuntimeRowKey,
                cancellationToken: cancellationToken);

            return ToRecord(response.Value);
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsNotFound(exception))
        {
            return null;
        }
    }

    public async Task<DiscountRuntimeRecord?> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        var filter = TableClient.CreateQueryFilter(
            $"RowKey eq {StorageConstants.CurrentRuntimeRowKey} and CorrelationId eq {correlationId}");

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           filter,
                           maxPerPage: 1,
                           cancellationToken: cancellationToken))
        {
            return ToRecord(entity);
        }

        return null;
    }

    public async Task<StorageWriteResult> UpsertCurrentAsync(
        DiscountRuntimeRecord record,
        CancellationToken cancellationToken)
    {
        await _tableClient.UpsertEntityAsync(ToEntity(record), TableUpdateMode.Replace, cancellationToken);
        return StorageWriteResult.Updated;
    }

    public async Task<StorageWriteResult> ReplaceCurrentAsync(
        DiscountRuntimeRecord record,
        string concurrencyToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await _tableClient.UpdateEntityAsync(
                ToEntity(record),
                new ETag(concurrencyToken),
                TableUpdateMode.Replace,
                cancellationToken);

            return StorageWriteResult.Updated;
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsNotFound(exception))
        {
            return StorageWriteResult.NotFound;
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsPreconditionFailed(exception))
        {
            return StorageWriteResult.PreconditionFailed;
        }
    }

    public async Task<PagedResult<DiscountRuntimeRecord>> ListUpdatedBeforeAsync(
        DateTimeOffset updatedBeforeUtc,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var filter = TableClient.CreateQueryFilter(
            $"RowKey eq {StorageConstants.CurrentRuntimeRowKey} and UpdatedAtUtc lt {updatedBeforeUtc}");

        var query = _tableClient
            .QueryAsync<TableEntity>(filter, maxPerPage: pageSize, cancellationToken: cancellationToken)
            .AsPages(continuationToken, pageSize);

        await foreach (var page in query.WithCancellation(cancellationToken))
        {
            return new PagedResult<DiscountRuntimeRecord>(
                page.Values.Select(ToRecord).ToArray(),
                page.ContinuationToken);
        }

        return new PagedResult<DiscountRuntimeRecord>([], null);
    }

    public async Task<StorageWriteResult> DeleteCurrentAsync(
        string phoneRuntimeKey,
        string concurrencyToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(
                BuildPartitionKey(phoneRuntimeKey),
                StorageConstants.CurrentRuntimeRowKey,
                new ETag(concurrencyToken),
                cancellationToken);

            return StorageWriteResult.Updated;
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsNotFound(exception))
        {
            return StorageWriteResult.NotFound;
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsPreconditionFailed(exception))
        {
            return StorageWriteResult.PreconditionFailed;
        }
    }

    private static string BuildPartitionKey(string phoneRuntimeKey) =>
        $"{StorageConstants.RuntimePartitionKeyPrefix}{phoneRuntimeKey}";

    private static string ExtractPhoneRuntimeKey(string partitionKey)
    {
        return partitionKey.StartsWith(StorageConstants.RuntimePartitionKeyPrefix, StringComparison.Ordinal)
            ? partitionKey[StorageConstants.RuntimePartitionKeyPrefix.Length..]
            : throw new InvalidOperationException("Discount runtime partition key is invalid.");
    }

    private static TableEntity ToEntity(DiscountRuntimeRecord record)
    {
        var entity = new TableEntity(BuildPartitionKey(record.PhoneRuntimeKey), StorageConstants.CurrentRuntimeRowKey)
        {
            [StorageConstants.Properties.Phone] = record.Phone.Value,
            [StorageConstants.Properties.CorrelationId] = record.CorrelationId,
            [StorageConstants.Properties.SmsCodeHash] = record.SmsCodeHash,
            [StorageConstants.Properties.SmsAttempts] = record.SmsAttempts,
            [StorageConstants.Properties.SmsMaxAttempts] = record.SmsMaxAttempts,
            [StorageConstants.Properties.SmsSentAtUtc] = record.SmsSentAtUtc,
            [StorageConstants.Properties.SmsExpiresAtUtc] = record.SmsExpiresAtUtc,
            [StorageConstants.Properties.CreatedAtUtc] = record.CreatedAtUtc,
            [StorageConstants.Properties.UpdatedAtUtc] = record.UpdatedAtUtc
        };

        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.PhoneVerifiedAtUtc, record.PhoneVerifiedAtUtc);
        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.BarcodeHash, record.BarcodeHash);
        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.BarcodeExpiresAtUtc, record.BarcodeExpiresAtUtc);
        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.BarcodeConsumedAtUtc, record.BarcodeConsumedAtUtc);
        TableStorageMapper.AddIfNotNull(entity, StorageConstants.Properties.ConsumedByScanId, record.ConsumedByScanId);

        return entity;
    }

    private static DiscountRuntimeRecord ToRecord(TableEntity entity)
    {
        var phoneValue = TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.Phone);

        if (!NormalizedPhoneNumber.TryCreate(phoneValue, out var phone))
        {
            throw new InvalidOperationException("Stored discount runtime phone is invalid.");
        }

        return new DiscountRuntimeRecord(
            ExtractPhoneRuntimeKey(entity.PartitionKey),
            phone,
            TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.CorrelationId),
            TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.SmsCodeHash),
            TableStorageMapper.GetRequiredInt32(entity, StorageConstants.Properties.SmsAttempts),
            TableStorageMapper.GetRequiredInt32(entity, StorageConstants.Properties.SmsMaxAttempts),
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.SmsSentAtUtc),
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.SmsExpiresAtUtc),
            TableStorageMapper.GetOptionalDateTimeOffset(entity, StorageConstants.Properties.PhoneVerifiedAtUtc),
            TableStorageMapper.GetOptionalString(entity, StorageConstants.Properties.BarcodeHash),
            TableStorageMapper.GetOptionalDateTimeOffset(entity, StorageConstants.Properties.BarcodeExpiresAtUtc),
            TableStorageMapper.GetOptionalDateTimeOffset(entity, StorageConstants.Properties.BarcodeConsumedAtUtc),
            TableStorageMapper.GetOptionalString(entity, StorageConstants.Properties.ConsumedByScanId),
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.CreatedAtUtc),
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.UpdatedAtUtc),
            TableStorageMapper.ToConcurrencyToken(entity.ETag));
    }
}
