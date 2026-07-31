using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleDiscountVerifier.Api.Application.Admin;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Application.Pos;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Application.Sms;
using SimpleDiscountVerifier.Api.Application.System;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Functions.Http;
using SimpleDiscountVerifier.Api.Infrastructure;
using SimpleDiscountVerifier.Api.Infrastructure.Options;
using SimpleDiscountVerifier.Api.Infrastructure.Sms;
using SimpleDiscountVerifier.Api.Infrastructure.Storage;
using SimpleDiscountVerifier.Api.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseMiddleware<GlobalExceptionMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddBackendOptions(context.Configuration);
        services.AddBackendStorage();
        services.AddSingleton<AdminCustomerProfileService>();
        services.AddSingleton<AdminSupportService>();
        services.AddSingleton<PosBarcodeValidationService>();
        services.AddSingleton<PosCustomerProfileLookupService>();
        services.AddSingleton<PosHmacAuthenticationService>();
        services.AddSingleton<PublicRedemptionService>();
        services.AddSingleton<SystemCleanupService>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ISmsSender, SmsFlyClient>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IScannerSurveyTableWriter, ScannerSurveyTableWriter>();
    })
    .Build();

host.Run();
