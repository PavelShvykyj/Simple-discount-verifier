using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Contracts.Public;
using SimpleDiscountVerifier.Api.Functions.Http;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Functions.Public;

public sealed class PublicRedemptionsFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PublicRedemptionService _redemptions;
    private readonly TurnstileOptions _turnstileOptions;
    private readonly ILogger<PublicRedemptionsFunction> _logger;

    public PublicRedemptionsFunction(
        PublicRedemptionService redemptions,
        IOptions<TurnstileOptions> turnstileOptions,
        ILogger<PublicRedemptionsFunction> logger)
    {
        _redemptions = redemptions;
        _turnstileOptions = turnstileOptions.Value;
        _logger = logger;
    }

    [Function(nameof(GetConfig))]
    public Task<HttpResponseData> GetConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/redemptions/config")]
        HttpRequestData request,
        CancellationToken cancellationToken) =>
        HttpResponseWriter.WriteJsonAsync(
            request,
            HttpStatusCode.OK,
            new
            {
                turnstileEnabled = _turnstileOptions.Enabled,
                turnstileSiteKey = _turnstileOptions.Enabled ? _turnstileOptions.SiteKey : null
            },
            cancellationToken);

    [Function(nameof(StartRedemption))]
    public async Task<HttpResponseData> StartRedemption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "public/redemptions")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<StartRedemptionRequest>(request, cancellationToken);

        if (!body.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, body.Error!, cancellationToken);
        }

        var result = await _redemptions.StartAsync(
            new StartRedemptionCommand(body.Value?.Phone, body.Value?.TurnstileToken),
            cancellationToken);

        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                ToResponse(result.Value!),
                cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    [Function(nameof(VerifySms))]
    public async Task<HttpResponseData> VerifySms(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "public/redemptions/{redemptionKey}/sms-verifications")]
        HttpRequestData request,
        string redemptionKey,
        CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<VerifySmsRequest>(request, cancellationToken);

        if (!body.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, body.Error!, cancellationToken);
        }

        if (!TryDecodePathSegment(redemptionKey, out var decodedRedemptionKey))
        {
            return await HttpResponseWriter.WriteErrorAsync(
                request,
                new ApplicationError(
                    RedemptionErrorCodes.InvalidRequest,
                    "Redemption key is missing or malformed.",
                    HttpStatusCode.BadRequest),
                cancellationToken);
        }

        var result = await _redemptions.VerifySmsAsync(
            new VerifySmsCommand(decodedRedemptionKey, body.Value?.Code),
            cancellationToken);

        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                ToResponse(result.Value!),
                cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    private async Task<ApplicationResult<T?>> ReadJsonAsync<T>(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<T>(
                request.Body,
                JsonOptions,
                cancellationToken);

            return ApplicationResult<T?>.Success(body);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Public redemption request body is invalid JSON.");
            return ApplicationResult<T?>.Failure(
                new ApplicationError(
                    RedemptionErrorCodes.InvalidRequest,
                    "Request body must be valid JSON.",
                    HttpStatusCode.BadRequest));
        }
    }

    private static StartRedemptionResponse ToResponse(StartRedemptionResult result) =>
        new(
            result.RedemptionKey,
            result.CorrelationId,
            result.SmsSent,
            result.RetryAfterSeconds,
            result.SmsExpiresAt);

    private static SmsVerificationResponse ToResponse(SmsVerificationResult result) =>
        new(
            result.CorrelationId,
            result.BarcodeValue,
            result.BarcodeFormat,
            result.ExpiresAt,
            result.TtlSeconds);

    private static bool TryDecodePathSegment(string value, out string decodedValue)
    {
        try
        {
            decodedValue = Uri.UnescapeDataString(value);
            return true;
        }
        catch (ArgumentException)
        {
            decodedValue = string.Empty;
            return false;
        }
    }
}
