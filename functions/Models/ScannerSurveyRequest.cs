using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Models;

public sealed class ScannerSurveyRequest
{
    [JsonPropertyName("branchName")]
    public string? BranchName { get; init; }

    [JsonPropertyName("submittedAtClient")]
    public DateTimeOffset? SubmittedAtClient { get; init; }

    [JsonPropertyName("terminals")]
    public IReadOnlyList<ScannerSurveyTerminal>? Terminals { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
}

