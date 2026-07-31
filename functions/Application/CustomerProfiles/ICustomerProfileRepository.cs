using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Application.CustomerProfiles;

public interface ICustomerProfileRepository
{
    Task<CustomerProfileRecord?> GetByPhoneAsync(
        NormalizedPhoneNumber phone,
        CancellationToken cancellationToken);

    Task<PagedResult<CustomerProfileRecord>> ListAsync(
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> InsertAsync(
        CustomerProfileRecord profile,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> ReplaceAsync(
        CustomerProfileRecord profile,
        string concurrencyToken,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> DeleteAsync(
        NormalizedPhoneNumber phone,
        string concurrencyToken,
        CancellationToken cancellationToken);

    Task<StorageWriteResult> ChangePhoneAsync(
        NormalizedPhoneNumber currentPhone,
        string currentConcurrencyToken,
        CustomerProfileRecord newProfile,
        CancellationToken cancellationToken);
}
