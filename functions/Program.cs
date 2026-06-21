using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure;
using SimpleDiscountVerifier.Api.Infrastructure.Options;
using SimpleDiscountVerifier.Api.Infrastructure.Storage;
using SimpleDiscountVerifier.Api.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddBackendOptions(context.Configuration);
        services.AddBackendStorage();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IScannerSurveyTableWriter, ScannerSurveyTableWriter>();
    })
    .Build();

host.Run();
