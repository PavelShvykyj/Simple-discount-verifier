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

public sealed class PosBarcodeValidationFunction
{
    private const string ClientIdHeaderName = "x-client-id";
    private const string TimestampHeaderName = "x-timestamp";
    private const string SignatureHeaderName = "x-signature";
    private const string LookupKeyTypePhone = "phone";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PosHmacAuthenticationService _authentication;
    private readonly PosBarcodeValidationService _validation;
    private readonly ILogger<PosBarcodeValidationFunction> _logger;

    public PosBarcodeValidationFunction(
        PosHmacAuthenticationService authentication,
        PosBarcodeValidationService validation,
        ILogger<PosBarcodeValidationFunction> logger)
    {
        _authentication = authentication;
        _validation = validation;
        _logger = logger;
    }

    [Function(nameof(ValidateBarcode))]
    public async Task<HttpResponseData> ValidateBarcode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pos/barcodes/validate")]
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

        var result = await _validation.ValidateAsync(
            new ValidatePosBarcodeCommand(
                body.Value?.BarcodeValue,
                body.Value?.TerminalId,
                body.Value?.BranchId,
                body.Value?.ScanId,
                authentication.Value!),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
        }

        return result.Value! switch
        {
            PosBarcodeValidationSuccessResult success => await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                new PosBarcodeValidationSuccessResponse(
                    success.Valid,
                    LookupKeyTypePhone,
                    success.LookupKey,
                    success.CorrelationId!,
                    success.ValidatedAt,
                    success.IdempotentReplay),
                cancellationToken),
            PosBarcodeValidationFailureResult failure => await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                new PosBarcodeValidationFailureResponse(
                    failure.Valid,
                    failure.Reason,
                    failure.CorrelationId,
                    failure.ValidatedAt),
                cancellationToken),
            _ => throw new InvalidOperationException("Unsupported POS barcode validation result.")
        };
    }

    private ApplicationResult<PosValidateBarcodeRequest?> ReadRequestBody(BinaryData body)
    {
        try
        {
            return ApplicationResult<PosValidateBarcodeRequest?>.Success(
                body.ToObjectFromJson<PosValidateBarcodeRequest>(JsonOptions));
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "POS barcode validation request body is invalid JSON.");
            return ApplicationResult<PosValidateBarcodeRequest?>.Failure(
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
