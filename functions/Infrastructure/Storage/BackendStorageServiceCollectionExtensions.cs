using Microsoft.Extensions.DependencyInjection;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Application.Redemptions;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

public static class BackendStorageServiceCollectionExtensions
{
    public static IServiceCollection AddBackendStorage(this IServiceCollection services)
    {
        services.AddSingleton<TableClientProvider>();
        services.AddSingleton<ICustomerProfileRepository, AzureTableCustomerProfileRepository>();
        services.AddSingleton<IDiscountRuntimeRepository, AzureTableDiscountRuntimeRepository>();
        services.AddSingleton<IAuditEventRepository, AzureTableAuditEventRepository>();
        services.AddSingleton<IAuditWriter, TableAuditWriter>();

        return services;
    }
}
