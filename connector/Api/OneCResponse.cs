using System.Text.Json;
using Sdv.Connector.Contracts;

namespace Sdv.Connector.Api;

/// <summary>Validates and maps a 1C lookup response to the Connector outcome contract.</summary>
internal static class OneCResponse
{
    internal const int MaxBodyBytes = 16384;

    /// <exception cref="InvalidDataException">The response violates the lookup contract.</exception>
    internal static LookupResult Normalize(int status, JsonElement body, string number)
    {
        StrictJson.Shape(body, ["contractVersion", "outcome", "reasonCode"], "order", "message");
        if (body.GetProperty("contractVersion").GetRawText() != "1") throw new InvalidDataException();
        if (body.TryGetProperty("message", out var message)) _ = StrictJson.Text(message);
        var outcome = StrictJson.Text(body.GetProperty("outcome"));
        var reason = StrictJson.Text(body.GetProperty("reasonCode"));
        var hasOrder = body.TryGetProperty("order", out var order);
        if (status == 200 && outcome == Outcomes.Found && reason == ReasonCodes.Success && hasOrder)
            return new(outcome, reason, OrderContract.ValidateOrder(order, number));
        if (status == 200 && outcome == Outcomes.NotFound && reason == ReasonCodes.OrderMissing && !hasOrder)
            return new(outcome, reason);
        if (outcome != Outcomes.Failed || hasOrder) throw new InvalidDataException();
        var valid = status switch
        {
            200 => reason is ReasonCodes.ApiUnavailable or ReasonCodes.ApiOutcomeUnknown,
            400 => reason is ReasonCodes.InvalidArguments or ReasonCodes.UnsupportedContractVersion,
            401 or 403 => reason == ReasonCodes.AccessDenied,
            408 => reason == ReasonCodes.Expired,
            404 or 405 or 413 or 415 => reason == ReasonCodes.InvalidArguments,
            500 or 503 => reason == ReasonCodes.ApiUnavailable,
            _ => false
        };
        if (!valid) throw new InvalidDataException();
        return reason switch
        {
            ReasonCodes.UnsupportedContractVersion => LookupResult.Failed(ReasonCodes.InvalidArguments, ApiErrors.ApiProtocolFailure),
            ReasonCodes.InvalidArguments => LookupResult.Failed(reason, "ApiRejected"),
            _ => LookupResult.Failed(reason)
        };
    }
}
