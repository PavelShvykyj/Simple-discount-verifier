using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.Pos;
using SimpleDiscountVerifier.Api.Contracts.Pos;
using SimpleDiscountVerifier.Api.Domain.Security;
using SimpleDiscountVerifier.Api.Functions.Http;

namespace SimpleDiscountVerifier.Api.Functions.Pos;

public sealed class PosCustomerProfileLookupFunction
{
    private const string ClientIdHeaderName = "x-client-id";
    private const string TimestampHeaderName = "x-timestamp";
    private const string SignatureHeaderName = "x-signature";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PosHmacAuthenticationService _authentication;
    private readonly PosCustomerProfileLookupService _lookup;
    private readonly ILogger<PosCustomerProfileLookupFunction> _logger;

    public PosCustomerProfileLookupFunction(
        PosHmacAuthenticationService authentication,
        PosCustomerProfileLookupService lookup,
        ILogger<PosCustomerProfileLookupFunction> logger)
    {
        _authentication = authentication;
        _lookup = lookup;
        _logger = logger;
    }

    [Function(nameof(LookupCustomerProfile))]
    public async Task<HttpResponseData> LookupCustomerProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pos/customer-profiles/lookup")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var requestBody = await BinaryData.FromStreamAsync(request.Body, cancellationToken);
        var bodyBytes = requestBody.ToArray();
        var authentication = _authentication.Authenticate(
            new PosAuthenticateCommand(
                request.Method,
                request.Url.AbsolutePath,
                GetHeader(request, TimestampHeaderName),
                SecretHasher.Sha256Hex(bodyBytes),
                GetHeader(request, ClientIdHeaderName),
                GetHeader(request, SignatureHeaderName)));

        if (!authentication.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, authentication.Error!, cancellationToken);
        }

        var body = ReadRequestBody(requestBody);

        if (!body.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, body.Error!, cancellationToken);
        }

        var result = await _lookup.LookupAsync(
            new LookupPosCustomerProfileCommand(
                body.Value?.Phone,
                body.Value?.TerminalId,
                body.Value?.BranchId,
                authentication.Value!),
            cancellationToken);

        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                new PosCustomerProfileLookupResponse(
                    result.Value!.Found,
                    result.Value.Profile!,
                    result.Value.ServedAt),
                cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    private ApplicationResult<PosCustomerProfileLookupRequest?> ReadRequestBody(BinaryData body)
    {
        try
        {
            return ApplicationResult<PosCustomerProfileLookupRequest?>.Success(
                body.ToObjectFromJson<PosCustomerProfileLookupRequest>(JsonOptions));
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "POS customer profile lookup request body is invalid JSON.");
            return ApplicationResult<PosCustomerProfileLookupRequest?>.Failure(
                new ApplicationError(
                    PosErrorCodes.InvalidRequest,
                    "Request body must be valid JSON.",
                    HttpStatusCode.BadRequest));
        }
    }

    private static string? GetHeader(HttpRequestData request, string headerName)
    {
        return request.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
