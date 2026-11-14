using System.Security.Cryptography;
using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Invocations;
using Azure.Core;
using Azure.Identity;
using OpenTelemetry.Trace;

namespace Sdv.Worker;

internal static class Program
{
    private static void Main(string[] args) => BuildHost(args).Run();

    internal static AgentHostApp BuildHost(string[] args)
    {
        var builder = AgentHost.CreateBuilder(args);
        builder.AddInvocations<Invocation>();
        var services = builder.Services;
        services.AddSingleton<IAuthority, HostedAuthority>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TokenCredential, ManagedIdentityCredential>();
        services.AddSingleton<Snapshot>();
        services.AddSingleton(sp => SettingsFrom(sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton(sp => WireFrom(sp.GetRequiredService<IConfiguration>()));
        services.AddScoped<IState>(sp => new FoundryState(sp.GetRequiredService<TokenCredential>(),
            Endpoint(sp.GetRequiredService<IConfiguration>(), "FOUNDRY_PROJECT_ENDPOINT")));
        services.AddSingleton<ITransport>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new AzureTransport(sp.GetRequiredService<TokenCredential>(), Required(config, "WORKER_SERVICE_BUS_NAMESPACE"),
                Endpoint(config, "WORKER_PUBSUB_ENDPOINT"), Required(config, "WORKER_PUBSUB_HUB"),
                sp.GetRequiredService<Settings>(), sp.GetRequiredService<TimeProvider>());
        });
        services.AddScoped<Lookup>();
        builder.ConfigureTracing(tracing => tracing.SetSampler(new AlwaysOffSampler()));
        builder.WebApplicationBuilder.Logging.ClearProviders();
        return builder.Build();
    }

    private static Settings SettingsFrom(IConfiguration config)
    {
        if (config["WORKER_PROFILE"] != "develop-synthetic") throw new InvalidOperationException();
        var queues = new Dictionary<string, string>();
        foreach (var branch in new[] { "101", "102" })
            if (config["WORKER_QUEUE_" + branch] is { } queue && Wire.Match(queue, "[A-Za-z0-9._-]{1,260}"))
                queues.Add(branch, queue);
        if (queues.Count == 0 || queues.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != queues.Count)
            throw new InvalidOperationException();
        return new(Required(config, "WORKER_AGENT"), Required(config, "WORKER_VERSION"), queues, new Dictionary<string, string>
        {
            ["lookup-fixture-00042"] = "00042", ["lookup-fixture-00044"] = "00044", ["lookup-fixture-99999"] = "99999"
        });
    }

    internal static Wire WireFrom(IConfiguration config)
    {
        RSA? backend = null;
        var fixture = RSA.Create();
        try
        {
            fixture.ImportFromPem(Required(config, "WORKER_TEST_PUBLIC_KEY"));
            if (fixture.KeySize < 2048) throw new InvalidOperationException();
            var keys = new Dictionary<string, RSA> { [Required(config, "WORKER_TEST_KID")] = fixture };
            var backendPem = config["WORKER_BACKEND_PRIVATE_KEY"];
            var backendKid = config["WORKER_BACKEND_KID"];
            if (backendPem is null && backendKid is null) return new(keys);
            if (string.IsNullOrEmpty(backendPem) || string.IsNullOrEmpty(backendKid))
                throw new InvalidOperationException("Incomplete backend signing key configuration.");
            backend = RSA.Create();
            backend.ImportFromPem(backendPem);
            if (backend.KeySize < 2048 ||
                backend.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(fixture.ExportSubjectPublicKeyInfo()))
                throw new InvalidOperationException();
            _ = backend.SignData("worker-key-check"u8, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            return new(keys, backendKid, backend);
        }
        catch
        {
            backend?.Dispose();
            fixture.Dispose();
            throw;
        }
    }

    private static Uri Endpoint(IConfiguration config, string key)
    {
        var endpoint = new Uri(Required(config, key), UriKind.Absolute);
        if (endpoint.Scheme != Uri.UriSchemeHttps || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0)
            throw new InvalidOperationException();
        return endpoint;
    }

    private static string Required(IConfiguration config, string key) =>
        config[key] is { Length: > 0 } value ? value : throw new InvalidOperationException();
}
