using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Models;

public sealed class ScannerSurveyTerminal
{
    [JsonPropertyName("terminalName")]
    public string? TerminalName { get; init; }

    [JsonPropertyName("answers")]
    public IReadOnlyList<ScannerSurveyAnswer>? Answers { get; init; }
}

