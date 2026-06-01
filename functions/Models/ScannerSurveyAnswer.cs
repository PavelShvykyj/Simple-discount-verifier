using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Models;

public sealed class ScannerSurveyAnswer
{
    [JsonPropertyName("barcodeId")]
    public string? BarcodeId { get; init; }

    [JsonPropertyName("isReadable")]
    public bool? IsReadable { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
}

