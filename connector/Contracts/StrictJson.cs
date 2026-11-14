using System.Globalization;
using System.Text;
using System.Text.Json;
using Sdv.Connector.Configuration;

namespace Sdv.Connector.Contracts;

/// <summary>Strict JSON primitives: no duplicates, no nulls, no unknown fields, strict UTF-8.</summary>
internal static class StrictJson
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    internal static string Text(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.String) throw new InvalidDataException();
        try { return e.GetString()!; }
        catch (InvalidOperationException) { throw new InvalidDataException(); }
    }

    internal static void Shape(JsonElement e, string[] required, params string[] optional)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in e.EnumerateObject())
        {
            string name;
            try { name = p.Name; }
            catch (InvalidOperationException) { throw new InvalidDataException(); }
            if (!seen.Add(name) || p.Value.ValueKind == JsonValueKind.Null || (!required.Contains(name) && !optional.Contains(name))) throw new InvalidDataException();
        }
        if (required.Any(x => !seen.Contains(x))) throw new InvalidDataException();
    }

    internal static JsonElement Parse(byte[] bytes, int limit)
    {
        if (bytes.Length > limit || bytes.AsSpan().StartsWith(Bom)) throw new InvalidDataException();
        _ = Utf8.GetString(bytes);
        using var d = JsonDocument.Parse(bytes);
        return d.RootElement.Clone();
    }

    static ReadOnlySpan<byte> Bom => [0xEF, 0xBB, 0xBF];

    internal static DateTimeOffset Time(JsonElement e)
    {
        var s = Text(e);
        if (!Patterns.Timestamp().IsMatch(s) || !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)) throw new InvalidDataException();
        return t;
    }

    internal static string Stamp(DateTimeOffset t) => t.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    internal static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string value)
    {
        if (!Patterns.Base64Url().IsMatch(value)) throw new InvalidDataException();
        var b = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
        if (ToBase64Url(b) != value) throw new InvalidDataException();
        return b;
    }
}
