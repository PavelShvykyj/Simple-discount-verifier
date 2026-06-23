using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SimpleDiscountVerifier.Api.Application.Admin;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Contracts.Admin;
using SimpleDiscountVerifier.Api.Functions.Http;

namespace SimpleDiscountVerifier.Api.Functions.Admin;

public sealed class AdminSupportFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminSupportService _support;
    private readonly ILogger<AdminSupportFunction> _logger;

    public AdminSupportFunction(
        AdminSupportService support,
        ILogger<AdminSupportFunction> logger)
    {
        _support = support;
        _logger = logger;
    }

    [Function(nameof(ListAuditEvents))]
    public async Task<HttpResponseData> ListAuditEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = AdminApiRoutes.BackofficeAuditEvents)]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = QueryHelpers.ParseQuery(request.Url.Query);
        int? pageSize = null;

        if (query.TryGetValue("pageSize", out var pageSizeValue)
            && int.TryParse(pageSizeValue.FirstOrDefault(), out var parsedPageSize))
        {
            pageSize = parsedPageSize;
        }

        var result = await _support.ListAuditEventsAsync(
            new AdminAuditEventsQuery(
                query.TryGetValue("correlationId", out var correlationId) ? correlationId.FirstOrDefault() : null,
                query.TryGetValue("phone", out var phone) ? phone.FirstOrDefault() : null,
                pageSize,
                query.TryGetValue("continuationToken", out var token) ? token.FirstOrDefault() : null),
            cancellationToken);

        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                new AuditEventListResponse(result.Value!.Items, result.Value.ContinuationToken),
                cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    [Function(nameof(InspectRedemption))]
    public async Task<HttpResponseData> InspectRedemption(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = AdminApiRoutes.BackofficeRedemptionsInspect)]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await ReadInspectRequestAsync(request, cancellationToken);

        if (!body.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, body.Error!, cancellationToken);
        }

        var result = await _support.InspectAsync(
            new AdminInspectCommand(body.Value?.CorrelationId, body.Value?.BarcodeValue),
            cancellationToken);

        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                ToResponse(result.Value!),
                cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    private async Task<ApplicationResult<RedemptionInspectRequest?>> ReadInspectRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<RedemptionInspectRequest>(
                request.Body,
                JsonOptions,
                cancellationToken);

            return ApplicationResult<RedemptionInspectRequest?>.Success(body);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Admin redemption inspect request body is invalid JSON.");
            return ApplicationResult<RedemptionInspectRequest?>.Failure(
                new ApplicationError(
                    AdminSupportErrorCodes.InvalidRequest,
                    "Request body must be valid JSON.",
                    HttpStatusCode.BadRequest));
        }
    }

    private static RedemptionInspectResponse ToResponse(AdminInspectResult result) =>
        new(
            result.CorrelationId,
            new RedemptionInspectSummaryResponse(
                result.Redemption.Status,
                result.Redemption.StartedAt,
                result.Redemption.LastEventAt),
            new BarcodeInspectSummaryResponse(
                result.Barcode.Value,
                result.Barcode.FormatValid,
                result.Barcode.PhoneRuntimeKey,
                result.Barcode.CorrelationId,
                result.Barcode.Status,
                result.Barcode.ExpiresAt,
                result.Barcode.ConsumedAt,
                result.Barcode.ConsumedByScanId),
            result.Scan is null
                ? null
                : new ScanInspectSummaryResponse(result.Scan.ScanId, result.Scan.Parsed),
            result.Profile,
            result.AuditEvents);
}
