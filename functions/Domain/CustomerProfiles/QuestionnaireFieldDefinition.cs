namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

public sealed record QuestionnaireFieldDefinition(
    string Code,
    string Name,
    QuestionnaireFieldType Type,
    bool Required,
    int? MinLength = null,
    int? MaxLength = null,
    string? Format = null)
{
    public const string DefaultDateFormat = "yyyy-MM-dd";
}
