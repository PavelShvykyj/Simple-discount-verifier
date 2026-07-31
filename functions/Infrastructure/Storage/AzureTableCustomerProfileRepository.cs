using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

public sealed class AzureTableCustomerProfileRepository : ICustomerProfileRepository
{
    private readonly TableClient _tableClient;

    public AzureTableCustomerProfileRepository(TableClientProvider tableClientProvider)
    {
        _tableClient = tableClientProvider.CreateCustomerProfilesClient();
    }

    public async Task<CustomerProfileRecord?> GetByPhoneAsync(
        NormalizedPhoneNumber phone,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                StorageConstants.CustomerProfilePartitionKey,
                phone.StorageKey,
                cancellationToken: cancellationToken);

            return ToRecord(response.Value);
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsNotFound(exception))
        {
            return null;
        }
    }

    public async Task<PagedResult<CustomerProfileRecord>> ListAsync(
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var query = _tableClient
            .QueryAsync<TableEntity>(
                entity => entity.PartitionKey == StorageConstants.CustomerProfilePartitionKey,
                maxPerPage: pageSize,
                cancellationToken: cancellationToken)
            .AsPages(continuationToken, pageSize);

        await foreach (var page in query.WithCancellation(cancellationToken))
        {
            return new PagedResult<CustomerProfileRecord>(
                page.Values.Select(ToRecord).ToArray(),
                page.ContinuationToken);
        }

        return new PagedResult<CustomerProfileRecord>([], null);
    }

    public async Task<StorageWriteResult> InsertAsync(
        CustomerProfileRecord profile,
        CancellationToken cancellationToken)
    {
        try
        {
            await _tableClient.AddEntityAsync(ToEntity(profile), cancellationToken);
            return StorageWriteResult.Created;
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsConflict(exception))
        {
            return StorageWriteResult.Conflict;
        }
    }

    public async Task<StorageWriteResult> ReplaceAsync(
        CustomerProfileRecord profile,
        string concurrencyToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await _tableClient.UpdateEntityAsync(
                ToEntity(profile),
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

    public async Task<StorageWriteResult> DeleteAsync(
        NormalizedPhoneNumber phone,
        string concurrencyToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(
                StorageConstants.CustomerProfilePartitionKey,
                phone.StorageKey,
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

    public async Task<StorageWriteResult> ChangePhoneAsync(
        NormalizedPhoneNumber currentPhone,
        string currentConcurrencyToken,
        CustomerProfileRecord newProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            var actions = new[]
            {
                new TableTransactionAction(TableTransactionActionType.Add, ToEntity(newProfile)),
                new TableTransactionAction(
                    TableTransactionActionType.Delete,
                    new TableEntity(StorageConstants.CustomerProfilePartitionKey, currentPhone.StorageKey)
                    {
                        ETag = new ETag(currentConcurrencyToken)
                    })
            };

            await _tableClient.SubmitTransactionAsync(actions, cancellationToken);
            return StorageWriteResult.Updated;
        }
        catch (RequestFailedException exception) when (TableStorageWriteResultMapper.IsConflict(exception))
        {
            return StorageWriteResult.Conflict;
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

    private static TableEntity ToEntity(CustomerProfileRecord profile)
    {
        return new TableEntity(StorageConstants.CustomerProfilePartitionKey, profile.Phone.StorageKey)
        {
            [StorageConstants.Properties.Phone] = profile.Phone.Value,
            [StorageConstants.Properties.PhysicalCardNumber] = profile.PhysicalCardNumber.Value,
            [StorageConstants.Properties.AnswersJson] = JsonSerializer.Serialize(profile.Answers, StorageJson.Options),
            [StorageConstants.Properties.CreatedAtUtc] = profile.CreatedAtUtc,
            [StorageConstants.Properties.UpdatedAtUtc] = profile.UpdatedAtUtc
        };
    }

    private static CustomerProfileRecord ToRecord(TableEntity entity)
    {
        var phoneValue = TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.Phone);

        if (!NormalizedPhoneNumber.TryCreate(phoneValue, out var phone))
        {
            throw new InvalidOperationException("Stored customer profile phone is invalid.");
        }

        var physicalCardNumberValue = TableStorageMapper.GetRequiredString(
            entity,
            StorageConstants.Properties.PhysicalCardNumber);

        if (!PhysicalCardNumber.TryCreate(physicalCardNumberValue, out var physicalCardNumber))
        {
            throw new InvalidOperationException("Stored customer profile physical card number is invalid.");
        }

        var answersJson = TableStorageMapper.GetRequiredString(entity, StorageConstants.Properties.AnswersJson);
        var answers = JsonSerializer.Deserialize<IReadOnlyList<QuestionnaireAnswer>>(
            answersJson,
            StorageJson.Options) ?? [];

        return new CustomerProfileRecord(
            phone,
            physicalCardNumber,
            answers,
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.CreatedAtUtc),
            TableStorageMapper.GetRequiredDateTimeOffset(entity, StorageConstants.Properties.UpdatedAtUtc),
            TableStorageMapper.ToConcurrencyToken(entity.ETag));
    }
}
