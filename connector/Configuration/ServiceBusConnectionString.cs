namespace Sdv.Connector.Configuration;

internal static class ServiceBusConnectionString
{
    internal static (string? Host, string? Error) Validate(string? connection, string? entity, string? policy)
    {
        if (string.IsNullOrWhiteSpace(connection)) return (null, "is required.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in connection.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || !fields.TryAdd(part[..separator], part[(separator + 1)..]))
                return (null, "must contain unique name=value entries.");
        }
        var endpoint = fields.GetValueOrDefault("Endpoint");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.HostNameType != UriHostNameType.Dns || endpoint != $"sb://{uri.Host}/")
            return (null, "must specify an Endpoint in the form sb://<namespace>/.");
        if (!fields.TryGetValue("EntityPath", out var actualEntity) || string.IsNullOrWhiteSpace(actualEntity))
            return (null, "must specify an EntityPath.");
        if (entity is not null && actualEntity != entity) return (null, $"must target the '{entity}' queue.");
        if (!fields.TryGetValue("SharedAccessKeyName", out var actualPolicy) || string.IsNullOrWhiteSpace(actualPolicy))
            return (null, "must specify a SharedAccessKeyName.");
        if (policy is not null && actualPolicy != policy) return (null, $"must use the '{policy}' policy.");
        Span<byte> keyBytes = stackalloc byte[32];
        if (!fields.TryGetValue("SharedAccessKey", out var key) ||
            !Convert.TryFromBase64String(key, keyBytes, out var written) || written != keyBytes.Length)
            return (null, "must specify a SharedAccessKey that decodes to 32 bytes.");
        if (fields.Count != 4) return (null, "contains unsupported connection string entries.");
        return (uri.Host, null);
    }
}
