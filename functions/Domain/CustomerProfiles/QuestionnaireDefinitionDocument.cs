using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

internal sealed class QuestionnaireDefinitionDocument
{
    public string? Version { get; init; }

    public IReadOnlyList<QuestionnaireFieldDefinitionDocument> Fields { get; init; } = [];
}

internal sealed class QuestionnaireFieldDefinitionDocument
{
    public string? Code { get; init; }

    public string? Name { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<QuestionnaireFieldType>))]
    public QuestionnaireFieldType Type { get; init; }

    public bool Required { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public string? Format { get; init; }
}
