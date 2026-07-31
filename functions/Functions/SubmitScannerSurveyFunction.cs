using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SimpleDiscountVerifier.Api.Models;
using SimpleDiscountVerifier.Api.Storage;

namespace SimpleDiscountVerifier.Api.Functions;

public sealed class SubmitScannerSurveyFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IScannerSurveyTableWriter _tableWriter;
    private readonly ILogger<SubmitScannerSurveyFunction> _logger;

    public SubmitScannerSurveyFunction(
        IScannerSurveyTableWriter tableWriter,
        ILogger<SubmitScannerSurveyFunction> logger)
    {
        _tableWriter = tableWriter;
        _logger = logger;
    }

    [Function(nameof(SubmitScannerSurvey))]
    public async Task<HttpResponseData> SubmitScannerSurvey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scanner-survey")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ScannerSurveyRequest? surveyRequest;

        try
        {
            surveyRequest = await JsonSerializer.DeserializeAsync<ScannerSurveyRequest>(
                request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Scanner survey request body is not valid JSON.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Request body must be valid JSON." }, cancellationToken);
            return badRequest;
        }

        surveyRequest ??= new ScannerSurveyRequest();

        var submissionId = Guid.NewGuid().ToString("N");
        var submittedAtUtc = DateTimeOffset.UtcNow;
        var userAgent = request.Headers.TryGetValues("User-Agent", out var userAgentValues)
            ? string.Join(" ", userAgentValues)
            : null;

        var rowsWritten = await _tableWriter.WriteAsync(
            surveyRequest,
            submissionId,
            submittedAtUtc,
            userAgent,
            cancellationToken);

        _logger.LogInformation(
            "Saved scanner survey submission {SubmissionId} with {RowsWritten} table rows.",
            submissionId,
            rowsWritten);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(
            new ScannerSurveySubmitResponse(submissionId, rowsWritten),
            cancellationToken);

        return response;
    }
}

