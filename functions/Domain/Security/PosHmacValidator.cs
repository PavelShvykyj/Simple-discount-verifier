using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SimpleDiscountVerifier.Api.Domain.Security;

public sealed record PosHmacValidationRequest(
    string Method,
    string Path,
    string Timestamp,
    string BodySha256Hex,
    string ClientId,
    string Signature);

public sealed record PosHmacValidationOptions(
    string ClientId,
    string HmacSecret,
    int FreshnessToleranceSeconds);

public static class PosHmacValidator
{
    public static string BuildCanonicalString(
        string method,
        string path,
        string timestamp,
        string bodySha256Hex)
    {
        return string.Join(
            '\n',
            method.ToUpperInvariant(),
            path,
            timestamp,
            bodySha256Hex.ToLowerInvariant());
    }

    public static string SignCanonicalString(string canonicalString, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));
        return Convert.ToBase64String(signature);
    }

    public static PosHmacValidationResult Validate(
        PosHmacValidationRequest request,
        PosHmacValidationOptions options,
        DateTimeOffset now)
    {
        if (!string.Equals(request.ClientId, options.ClientId, StringComparison.Ordinal))
        {
            return PosHmacValidationResult.Invalid(PosHmacValidationFailure.UnknownClient);
        }

        if (!DateTimeOffset.TryParse(
            request.Timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var timestamp))
        {
            return PosHmacValidationResult.Invalid(PosHmacValidationFailure.InvalidTimestamp);
        }

        var tolerance = TimeSpan.FromSeconds(options.FreshnessToleranceSeconds);
        var age = now - timestamp;

        if (age.Duration() > tolerance)
        {
            return PosHmacValidationResult.Invalid(PosHmacValidationFailure.StaleTimestamp);
        }

        var canonicalString = BuildCanonicalString(
            request.Method,
            request.Path,
            request.Timestamp,
            request.BodySha256Hex);

        var expectedSignature = SignCanonicalString(canonicalString, options.HmacSecret);

        return FixedTimeBase64Equals(expectedSignature, request.Signature)
            ? PosHmacValidationResult.Success(canonicalString)
            : PosHmacValidationResult.Invalid(PosHmacValidationFailure.InvalidSignature);
    }

    private static bool FixedTimeBase64Equals(string expected, string actual)
    {
        Span<byte> expectedBytes = stackalloc byte[32];
        Span<byte> actualBytes = stackalloc byte[32];

        if (!Convert.TryFromBase64String(expected, expectedBytes, out var expectedWritten)
            || !Convert.TryFromBase64String(actual, actualBytes, out var actualWritten)
            || expectedWritten != actualWritten)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            expectedBytes[..expectedWritten],
            actualBytes[..actualWritten]);
    }
}
