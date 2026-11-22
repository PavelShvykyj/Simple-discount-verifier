using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Invocations;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Sdv.Worker;

internal static class Program
{
    internal const string LogCategory = "Sdv.Worker.Program";

    private static async Task<int> Main(string[] args)
    {
        AgentHostApp host;
        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            host = BuildHost(args);
        }
        catch (Exception error)
        {
            // The logging pipeline is not available if host construction fails.
            Console.Error.WriteLine($"WorkerBootstrapFailed ErrorType={error.GetType().FullName} StackTrace={error.StackTrace}");
            return 1;
        }

        await using var app = host.App;
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory);
        try
        {
            await app.RunAsync();
            return 0;
        }
        catch (OptionsValidationException error)
        {
            foreach (var failure in error.Failures) WorkerLog.InvalidConfiguration(log, failure);
            return 1;
        }
        catch (Exception error)
        {
            WorkerLog.Terminated(log, error);
            return 1;
        }
    }

    internal static AgentHostApp BuildHost(string[] args)
    {
        var builder = AgentHost.CreateBuilder(args);
        builder.AddInvocations<Invocation>();
        var services = builder.Services;
        services.AddOptions<WorkerOptions>()
            .BindConfiguration(WorkerOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<WorkerOptions>, WorkerOptionsValidator>();
        services.AddSingleton(_ => new FoundryHostIdentity(
            FoundryEnvironment.IsHosted, FoundryEnvironment.AgentName, FoundryEnvironment.AgentVersion, FoundryEnvironment.SessionId));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TokenCredential, ManagedIdentityCredential>();
        services.AddSingleton<Snapshot>();
        services.AddSingleton(sp => new Wire(sp.GetRequiredService<IOptions<WorkerOptions>>().Value));
        services.AddScoped<IState>(sp => new FoundryState(sp.GetRequiredService<TokenCredential>(),
            new Uri(sp.GetRequiredService<IOptions<WorkerOptions>>().Value.ProjectEndpoint),
            sp.GetRequiredService<ILogger<FoundryState>>()));
        services.AddSingleton<ITransport>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WorkerOptions>>().Value;
            return new AzureTransport(sp.GetRequiredService<TokenCredential>(), options.ServiceBusNamespace,
                new Uri(options.PubSubEndpoint), options.PubSubHub, options,
                sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<AzureTransport>>());
        });
        services.AddScoped<Lookup>();

        var host = builder.Build();
        var log = host.App.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory);
        host.App.Lifetime.ApplicationStarted.Register(() => WorkerLog.Started(log));
        host.App.Lifetime.ApplicationStopped.Register(() => WorkerLog.Stopped(log));
        return host;
    }
}
