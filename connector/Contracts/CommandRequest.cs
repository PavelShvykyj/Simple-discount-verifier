using System.Text.Json;

namespace Sdv.Connector.Contracts;

internal static class Outcomes
{
    internal const string Found = "Found";
    internal const string NotFound = "NotFound";
    internal const string Failed = "Failed";
}

internal static class ReasonCodes
{
    internal const string Success = "Success";
    internal const string OrderMissing = "OrderMissing";
    internal const string InvalidArguments = "InvalidArguments";
    internal const string UnsupportedContractVersion = "UnsupportedContractVersion";
    internal const string AccessDenied = "AccessDenied";
    internal const string Expired = "Expired";
    internal const string ApiUnavailable = "ApiUnavailable";
    internal const string ApiOutcomeUnknown = "ApiOutcomeUnknown";
}

internal sealed record LookupResult(string Outcome, string Reason, JsonElement? Order = null, string? Diagnostic = null)
{
    internal static LookupResult Failed(string reason, string? diagnostic = null) => new(Outcomes.Failed, reason, Diagnostic: diagnostic);
}

internal sealed record CommandRequest(JsonElement Envelope, DateTimeOffset Expiry, string? OrderNumber, JsonElement? AmountHint, string? Rejection)
{
    internal string Id => StrictJson.Text(Envelope.GetProperty("commandId"));
    internal string BranchId => StrictJson.Text(Envelope.GetProperty("branchId"));
    internal string ExpiresAtText => StrictJson.Text(Envelope.GetProperty("expiresAt"));
}
