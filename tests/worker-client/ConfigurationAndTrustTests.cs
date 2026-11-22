using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Worker;
using WorkerChecks;
using Xunit;

public sealed class ConfigurationAndTrustTests : IDisposable
{
    private readonly RSA swa = RSA.Create(2048);
    private readonly RSA router = RSA.Create(2048);
    private readonly WorkerOptionsValidator validator = new(new FoundryHostIdentity(false, null, null, null));

    private WorkerOptions ValidOptions() => new()
    {
        Agent = "test-agent", Version = "test-version",
        ProjectEndpoint = "https://project.example.invalid/api/projects/test",
        ServiceBusNamespace = "bus.example.invalid",
        PubSubEndpoint = "https://pubsub.example.invalid", PubSubHub = "test_hub",
        Queues = new() { ["303"] = "commands303", ["404"] = "commands404" },
        SwaPublicKeys = new() { ["fixture"] = swa.ExportSubjectPublicKeyInfoPem() },
        RouterPublicKeys = new() { ["router"] = router.ExportSubjectPublicKeyInfoPem() }
    };

    [Theory]
    [InlineData("other-agent", "test-version", "Worker:Agent")]
    [InlineData("test-agent", "other-version", "Worker:Version")]
    public void HostedAgentIdentityMustMatchConfiguredValues(string agent, string version, string setting)
    {
        var hostedValidator = new WorkerOptionsValidator(new FoundryHostIdentity(true, agent, version, null));
        var result = hostedValidator.Validate(null, ValidOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains(setting, StringComparison.Ordinal));
    }

    [Fact]
    public void BranchesAreConfiguredRatherThanFixedInTheExecutable()
    {
        Assert.True(validator.Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData("bad queue")]
    [InlineData("")]
    [InlineData("commands303")]
    public void InvalidSecondQueueIsNotSilentlyDropped(string queue)
    {
        var options = ValidOptions();
        options.Queues["404"] = queue;
        var result = validator.Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("Worker:Queues", StringComparison.Ordinal));
    }

    [Fact]
    public void PrivateKeyIsRejectedWithoutDisclosingItsValue()
    {
        var options = ValidOptions();
        var pem = swa.ExportPkcs8PrivateKeyPem();
        options.SwaPublicKeys["fixture"] = pem;
        var result = validator.Validate(null, options);
        Assert.True(result.Failed);
        Assert.All(result.Failures!, failure => Assert.DoesNotContain(pem, failure));
    }

    [Fact]
    public void RouterCannotUseTheSwaKey()
    {
        var options = ValidOptions();
        options.RouterPublicKeys["router"] = swa.ExportSubjectPublicKeyInfoPem();
        Assert.True(validator.Validate(null, options).Failed);
    }

    [Fact]
    public async Task InvalidConfigurationFailsBeforeTheHostStarts()
    {
        await using var host = Sdv.Worker.Program.BuildHost(["--Worker:Agent="]).App;
        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(error.Failures, failure => failure.Contains("Worker:Agent", StringComparison.Ordinal));
    }

    [Fact]
    public void ResultRequiresRouterSignatureOverTheActualBytes()
    {
        using var wire = new Wire(ValidOptions());
        var clock = new Clock();
        var fixtures = new Fixtures(swa, clock, router);
        var scope = new Scope("test-owner", "test-session");
        var caller = new Caller(scope.Owner, scope.Session, "test-call");
        var start = fixtures.Input(scope, Ingress.Start, text: "12345", branch: "303");
        var envelope = wire.Read(JsonSerializer.SerializeToElement(start), caller, clock.GetUtcNow());
        var command = Wire.CreateCommand(envelope, "12345", clock.GetUtcNow());
        var result = fixtures.Result(JsonNode.Parse(command.Body)!.AsObject(), Fixtures.Missing);
        Assert.Equal(result["eventId"]!.ToString(),
            wire.Read(JsonSerializer.SerializeToElement(result), caller, clock.GetUtcNow()).EventId);

        var changed = Fixtures.Copy(result);
        changed["dataB64"] = Fixtures.B64(JsonSerializer.SerializeToUtf8Bytes(Fixtures.Unknown));
        Assert.Throws<InvalidDataException>(() => wire.Read(JsonSerializer.SerializeToElement(changed), caller, clock.GetUtcNow()));

        var unproven = Fixtures.Copy(result);
        unproven.Remove("resultProof");
        Assert.Throws<InvalidDataException>(() => wire.Read(JsonSerializer.SerializeToElement(unproven), caller, clock.GetUtcNow()));

        var wrongSigner = new Fixtures(swa, clock, swa).Result(JsonNode.Parse(command.Body)!.AsObject(), Fixtures.Missing);
        Assert.Throws<InvalidDataException>(() => wire.Read(JsonSerializer.SerializeToElement(wrongSigner), caller, clock.GetUtcNow()));
    }

    [Fact]
    public void AccessToDisposedJsonIsNotClassifiedAsInvalidInput()
    {
        var document = JsonDocument.Parse("""{"kind":"session.prepare"}""");
        var root = document.RootElement;
        document.Dispose();

        var error = Assert.Throws<ObjectDisposedException>(() => Wire.Text(root, "kind"));
        Assert.False(Wire.Invalid(error));
    }

    public void Dispose()
    {
        swa.Dispose();
        router.Dispose();
    }

    [Fact]
    public async Task LoggingLevelsComeFromConfiguration()
    {
        await using var host = Sdv.Worker.Program.BuildHost([
            "--Logging:LogLevel:Default=None",
            "--Logging:LogLevel:Sdv.Worker.Program=Warning"]).App;
        var capture = new CapturingLoggerProvider();
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        factory.AddProvider(capture);
        factory.CreateLogger("Azure.AI.AgentServer.Invocations").LogError("sensitive SDK content");
        var log = factory.CreateLogger(Sdv.Worker.Program.LogCategory);
        log.LogInformation("WorkerStarted");
        log.LogWarning("RequestFailed");
        Assert.Equal(new[] { "RequestFailed" }, capture.Messages);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        internal List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }
}
