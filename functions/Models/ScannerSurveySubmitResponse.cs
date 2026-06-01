using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Models;

public sealed class ScannerSurveySubmitResponse
{
    public ScannerSurveySubmitResponse(string submissionId, int rowsWritten)
    {
        SubmissionId = submissionId;
        RowsWritten = rowsWritten;
    }

    [JsonPropertyName("submissionId")]
    public string SubmissionId { get; }

    [JsonPropertyName("rowsWritten")]
    public int RowsWritten { get; }
}

