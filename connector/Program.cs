using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Connector.Api;
using Sdv.Connector.Configuration;
using Sdv.Connector.Management;
using Sdv.Connector.Messaging;
using Sdv.Connector.Telegram;
using Serilog;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

HostApplicationBuilder builder;
try { builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory }); }
catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or FormatException)
{
    Console.Error.WriteLine($"Unable to load Connector settings from '{AppContext.BaseDirectory}'. Check JSON syntax and file access.");
    return 1;
}

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
try
{
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    var serviceName = builder.Configuration[$"{ConnectorOptions.SectionName}:ServiceName"] ?? "";
    builder.Services.AddWindowsService(o => o.ServiceName = serviceName);
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(35));

    builder.Services.AddOptions<ConnectorOptions>().BindConfiguration(ConnectorOptions.SectionName);
    builder.Services.AddSingleton<IValidateOptions<ConnectorOptions>, ConnectorOptionsValidator>();
    builder.Services.AddSingleton(TimeProvider.System);

    builder.Services.AddHttpClient(OneCApiService.HttpClientName)
        .ConfigureHttpClient((services, client) =>
        {
            client.BaseAddress = new Uri(services.GetRequiredService<IOptions<ConnectorOptions>>().Value.Api.BaseUri.TrimEnd('/') + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        })
        .RemoveAllLoggers();

    // Request logging is removed: Telegram URLs contain the bot token.
    builder.Services.AddHttpClient(TelegramBotService.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        })
        .RemoveAllLoggers();

    builder.Services.AddSingleton<ListenerHealth>();
    builder.Services.AddSingleton<IOneCApi, OneCApiService>();
    builder.Services.AddSingleton<IResultPublisher, ResultPublisher>();
    builder.Services.AddSingleton<CommandProcessor>();
    builder.Services.AddSingleton<ICommandListener, CommandListener>();
    builder.Services.AddSingleton<ManagementService>();
    builder.Services.AddSingleton<IManagementService>(services => services.GetRequiredService<ManagementService>());
    builder.Services.AddHostedService<ConnectorHostedService>();
    builder.Services.AddHostedService<TelegramBotService>();

    using var host = builder.Build();
    try
    {
        _ = host.Services.GetRequiredService<IOptions<ConnectorOptions>>().Value;
    }
    catch (OptionsValidationException error)
    {
        foreach (var failure in error.Failures)
        {
            Console.Error.WriteLine(failure);
            Log.Fatal("Invalid Connector settings: {Failure}", failure);
        }
        return 1;
    }
    catch (InvalidOperationException error)
    {
        Console.Error.WriteLine($"Connector settings could not be read: {error.Message}");
        Log.Fatal("Connector settings could not be read: {Message}", error.Message);
        return 1;
    }

    await host.RunAsync();
    return Environment.ExitCode;
}
catch (Exception error)
{
    Log.Fatal(error, "Connector terminated unexpectedly");
    Console.Error.WriteLine("ConnectorStartupOrRuntimeFailed");
    return 1;
}
finally { await Log.CloseAndFlushAsync(); }
