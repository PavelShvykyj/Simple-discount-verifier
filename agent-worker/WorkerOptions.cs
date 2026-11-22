using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Sdv.Worker;

internal sealed class WorkerOptions
{
    public WorkerOptions() { }
    internal const string SectionName = "Worker";
    internal const int MinimumRsaKeySize = 2048;
    internal const string BranchPattern = "[0-9]{1,128}";
    internal const string OrderNumberPattern = "[0-9]{1,64}";
    internal const string QueuePattern = "[A-Za-z0-9._-]{1,260}";

    public string Agent { get; set; } = "";
    public string Version { get; set; } = "";
    public string ProjectEndpoint { get; set; } = "";
    public string ServiceBusNamespace { get; set; } = "";
    public string PubSubEndpoint { get; set; } = "";
    public string PubSubHub { get; set; } = "";
    public Dictionary<string, string> Queues { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> SwaPublicKeys { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> RouterPublicKeys { get; set; } = new(StringComparer.Ordinal);

    internal static RSA ReadPublicKey(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem) || !PemEncoding.TryFind(pem, out var fields) ||
            !(pem.AsSpan()[fields.Label].SequenceEqual("PUBLIC KEY") ||
              pem.AsSpan()[fields.Label].SequenceEqual("RSA PUBLIC KEY")))
            throw new CryptographicException("Only public keys are accepted.");
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(pem);
            if (key.KeySize < MinimumRsaKeySize) throw new CryptographicException("RSA key is too small.");
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }
}

internal sealed class WorkerOptionsValidator(FoundryHostIdentity host) : IValidateOptions<WorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkerOptions options)
    {
        var errors = new List<string>();
        Required(options.Agent, "Agent");
        Required(options.Version, "Version");
        if (host.IsHosted)
        {
            if (options.Agent != host.AgentName)
                errors.Add("Worker:Agent does not match the Foundry agent name.");
            if (options.Version != host.AgentVersion)
                errors.Add("Worker:Version does not match the Foundry agent version.");
        }
        Endpoint(options.ProjectEndpoint, "ProjectEndpoint");
        Endpoint(options.PubSubEndpoint, "PubSubEndpoint");
        if (string.IsNullOrWhiteSpace(options.ServiceBusNamespace) ||
            Uri.CheckHostName(options.ServiceBusNamespace) != UriHostNameType.Dns || !options.ServiceBusNamespace.Contains('.'))
            errors.Add("Worker:ServiceBusNamespace must be a fully qualified DNS hostname without scheme or path.");
        if (!Wire.Match(options.PubSubHub, "[A-Za-z][A-Za-z0-9_]{0,127}"))
            errors.Add("Worker:PubSubHub must start with a letter and contain up to 128 letters, digits or underscores.");
        if (options.Queues.Count == 0) errors.Add("Worker:Queues must contain at least one branch-to-queue mapping.");
        foreach (var (branch, queue) in options.Queues)
            if (!Wire.Match(branch, WorkerOptions.BranchPattern) || !Wire.Match(queue, WorkerOptions.QueuePattern))
                errors.Add("Worker:Queues contains an invalid branch or queue name.");
        if (options.Queues.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Queues.Count)
            errors.Add("Worker:Queues must use different queues for different branches.");
        var swa = Keys(options.SwaPublicKeys, "SwaPublicKeys");
        var router = Keys(options.RouterPublicKeys, "RouterPublicKeys");
        if (swa.Overlaps(router)) errors.Add("Worker:SwaPublicKeys and Worker:RouterPublicKeys must use different key material.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);

        void Required(string value, string setting)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"Worker:{setting} is required.");
        }

        void Endpoint(string value, string setting)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                errors.Add($"Worker:{setting} must be an absolute HTTPS URL without credentials, query or fragment.");
        }

        HashSet<string> Keys(Dictionary<string, string> keys, string setting)
        {
            var material = new HashSet<string>(StringComparer.Ordinal);
            if (keys.Count == 0) errors.Add($"Worker:{setting} must contain at least one public verification key.");
            foreach (var (kid, pem) in keys)
            {
                if (!Wire.IsId(kid)) errors.Add($"Worker:{setting} contains an invalid key ID.");
                try
                {
                    using var key = WorkerOptions.ReadPublicKey(pem);
                    material.Add(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
                }
                catch (Exception error) when (error is CryptographicException or ArgumentException)
                {
                    errors.Add($"Worker:{setting} contains an invalid public RSA key; minimum size is {WorkerOptions.MinimumRsaKeySize} bits.");
                }
            }
            return material;
        }
    }
}
