using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.System;
using SimpleDiscountVerifier.Api.Contracts.System;
using SimpleDiscountVerifier.Api.Functions.Http;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Functions.System;

public sealed class SystemCleanupFunction
{
    private const string CleanupKeyHeaderName = "x-cleanup-key";

    private readonly SystemCleanupService _cleanupService;
    private readonly RuntimeOptions _runtimeOptions;

    public SystemCleanupFunction(
        SystemCleanupService cleanupService,
        IOptions<RuntimeOptions> runtimeOptions)
    {
        _cleanupService = cleanupService;
        _runtimeOptions = runtimeOptions.Value;
    }

    [Function(nameof(RunBackofficeCleanup))]
    public async Task<HttpResponseData> RunBackofficeCleanup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "backoffice/system/cleanup")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var result = await _cleanupService.RunAsync(cancellationToken);
        return await WriteCleanupResultAsync(request, result, cancellationToken);
    }

    [Function(nameof(RunMaintenanceCleanup))]
    public async Task<HttpResponseData> RunMaintenanceCleanup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "system/maintenance/cleanup")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!IsValidCleanupKey(GetHeader(request, CleanupKeyHeaderName)))
        {
            return await HttpResponseWriter.WriteErrorAsync(
                request,
                SystemCleanupService.Unauthorized(),
                cancellationToken);
        }

        var result = await _cleanupService.RunAsync(cancellationToken);
        return await WriteCleanupResultAsync(request, result, cancellationToken);
    }

    private static async Task<HttpResponseData> WriteCleanupResultAsync(
        HttpRequestData request,
        ApplicationResult<SystemCleanupResult> result,
        CancellationToken cancellationToken)
    {
        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(
                request,
                HttpStatusCode.OK,
                ToResponse(result.Value!),
                cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    private bool IsValidCleanupKey(string? suppliedKey)
    {
        if (string.IsNullOrWhiteSpace(suppliedKey)
            || string.IsNullOrWhiteSpace(_runtimeOptions.CleanupAutomationKey))
        {
            return false;
        }

        return FixedTimeEquals(_runtimeOptions.CleanupAutomationKey, suppliedKey);
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static CleanupResponse ToResponse(SystemCleanupResult result) =>
        new(
            result.RanAt,
            ToResponse(result.DiscountRuntime),
            ToResponse(result.AuditEvents));

    private static CleanupTableResponse ToResponse(CleanupTableResult result) =>
        new(
            result.CutoffUtc,
            result.Scanned,
            result.Deleted,
            result.Skipped,
            result.Failed);

    private static string? GetHeader(HttpRequestData request, string headerName)
    {
        return request.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
