using System.Globalization;
using SimpleDiscountVerifier.Api.Domain.Shared;

namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

public sealed class QuestionnaireDefinition
{
    public const string DefinitionFileName = "questionnaire.definition.json";
    public const string OutputRelativePath = "Domain/CustomerProfiles/" + DefinitionFileName;

    private static readonly Lazy<QuestionnaireDefinition> DefaultDefinition =
        new(QuestionnaireDefinitionLoader.LoadDefault);

    private readonly Dictionary<string, QuestionnaireFieldDefinition> _fieldsByCode;

    public QuestionnaireDefinition(string version, IReadOnlyList<QuestionnaireFieldDefinition> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (fields.Count == 0)
        {
            throw new ArgumentException("Questionnaire definition must contain at least one field.", nameof(fields));
        }

        Version = version;
        Fields = fields;
        _fieldsByCode = fields.ToDictionary(field => field.Code, StringComparer.Ordinal);

        if (_fieldsByCode.Count != fields.Count)
        {
            throw new ArgumentException("Questionnaire definition contains duplicate field codes.", nameof(fields));
        }
    }

    public string Version { get; }

    public IReadOnlyList<QuestionnaireFieldDefinition> Fields { get; }

    public static QuestionnaireDefinition Default => DefaultDefinition.Value;

    public static IReadOnlyList<QuestionnaireFieldDefinition> GetFields() => Default.Fields;

    public static DomainValidationResult<IReadOnlyList<QuestionnaireAnswer>> Validate(
        IEnumerable<QuestionnaireInputAnswer>? answers)
    {
        return Default.ValidateAnswers(answers);
    }

    public DomainValidationResult<IReadOnlyList<QuestionnaireAnswer>> ValidateAnswers(
        IEnumerable<QuestionnaireInputAnswer>? answers)
    {
        if (answers is null)
        {
            return DomainValidationResult<IReadOnlyList<QuestionnaireAnswer>>.Failure(
                QuestionnaireValidationErrorCodes.AnswersRequired);
        }

        var errors = new List<string>();
        var supplied = BuildSuppliedAnswerMap(answers, errors);
        var normalized = new List<QuestionnaireAnswer>(Fields.Count);

        foreach (var field in Fields)
        {
            supplied.TryGetValue(field.Code, out var rawValue);
            var value = string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();

            ValidateFieldValue(field, value, errors);
            normalized.Add(new QuestionnaireAnswer(field.Code, field.Name, value));
        }

        return errors.Count == 0
            ? DomainValidationResult<IReadOnlyList<QuestionnaireAnswer>>.Success(normalized)
            : DomainValidationResult<IReadOnlyList<QuestionnaireAnswer>>.Failure([.. errors]);
    }

    private Dictionary<string, string?> BuildSuppliedAnswerMap(
        IEnumerable<QuestionnaireInputAnswer> answers,
        List<string> errors)
    {
        var supplied = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var answer in answers)
        {
            if (string.IsNullOrWhiteSpace(answer.Code))
            {
                errors.Add(QuestionnaireValidationErrorCodes.AnswerCodeRequired);
                continue;
            }

            if (!_fieldsByCode.ContainsKey(answer.Code))
            {
                errors.Add(QuestionnaireValidationErrorCodes.WithCode(
                    QuestionnaireValidationErrorCodes.UnknownAnswerCode,
                    answer.Code));
                continue;
            }

            if (!supplied.TryAdd(answer.Code, answer.Value))
            {
                errors.Add(QuestionnaireValidationErrorCodes.WithCode(
                    QuestionnaireValidationErrorCodes.DuplicateAnswerCode,
                    answer.Code));
            }
        }

        return supplied;
    }

    private static void ValidateFieldValue(
        QuestionnaireFieldDefinition field,
        string? value,
        List<string> errors)
    {
        if (field.Required && value is null)
        {
            errors.Add(QuestionnaireValidationErrorCodes.WithCode(
                QuestionnaireValidationErrorCodes.AnswerRequired,
                field.Code));
            return;
        }

        if (value is null)
        {
            return;
        }

        if (field.MinLength is not null && value.Length < field.MinLength)
        {
            errors.Add(QuestionnaireValidationErrorCodes.WithCode(
                QuestionnaireValidationErrorCodes.InvalidAnswerFormat,
                field.Code));
            return;
        }

        if (field.MaxLength is not null && value.Length > field.MaxLength)
        {
            errors.Add(QuestionnaireValidationErrorCodes.WithCode(
                QuestionnaireValidationErrorCodes.InvalidAnswerFormat,
                field.Code));
            return;
        }

        if (field.Type == QuestionnaireFieldType.Date && !IsDateInConfiguredFormat(value, field.Format))
        {
            errors.Add(QuestionnaireValidationErrorCodes.WithCode(
                QuestionnaireValidationErrorCodes.InvalidAnswerFormat,
                field.Code));
        }
    }

    private static bool IsDateInConfiguredFormat(string value, string? format)
    {
        var expectedFormat = string.IsNullOrWhiteSpace(format)
            ? QuestionnaireFieldDefinition.DefaultDateFormat
            : format;

        return DateOnly.TryParseExact(
            value,
            expectedFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            && parsed.ToString(expectedFormat, CultureInfo.InvariantCulture) == value;
    }
}
