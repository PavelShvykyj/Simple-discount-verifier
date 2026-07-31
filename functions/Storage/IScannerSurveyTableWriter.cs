using SimpleDiscountVerifier.Api.Models;

namespace SimpleDiscountVerifier.Api.Storage;

public interface IScannerSurveyTableWriter
{
    Task<int> WriteAsync(
        ScannerSurveyRequest request,
        string submissionId,
        DateTimeOffset submittedAtUtc,
        string? userAgent,
        CancellationToken cancellationToken);
}

