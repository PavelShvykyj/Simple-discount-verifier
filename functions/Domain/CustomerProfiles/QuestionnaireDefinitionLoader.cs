using System.Text.Json;

namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

public static class QuestionnaireDefinitionLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static QuestionnaireDefinition LoadDefault()
    {
        var path = Path.Combine(AppContext.BaseDirectory, QuestionnaireDefinition.OutputRelativePath);
        return LoadFromFile(path);
    }

    public static QuestionnaireDefinition LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static QuestionnaireDefinition Load(Stream stream)
    {
        var document = JsonSerializer.Deserialize<QuestionnaireDefinitionDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Questionnaire definition file is empty.");

        if (string.IsNullOrWhiteSpace(document.Version))
        {
            throw new InvalidOperationException("Questionnaire definition must specify a version.");
        }

        if (document.Fields is null)
        {
            throw new InvalidOperationException("Questionnaire definition must specify fields.");
        }

        var fields = document.Fields.Select(ToFieldDefinition).ToArray();
        return new QuestionnaireDefinition(document.Version, fields);
    }

    private static QuestionnaireFieldDefinition ToFieldDefinition(QuestionnaireFieldDefinitionDocument field)
    {
        if (string.IsNullOrWhiteSpace(field.Code))
        {
            throw new InvalidOperationException("Questionnaire field code is required.");
        }

        if (string.IsNullOrWhiteSpace(field.Name))
        {
            throw new InvalidOperationException($"Questionnaire field '{field.Code}' must specify a name.");
        }

        if (field.MinLength is < 0)
        {
            throw new InvalidOperationException($"Questionnaire field '{field.Code}' has invalid minLength.");
        }

        if (field.MaxLength is < 0)
        {
            throw new InvalidOperationException($"Questionnaire field '{field.Code}' has invalid maxLength.");
        }

        if (field.MinLength is not null
            && field.MaxLength is not null
            && field.MinLength > field.MaxLength)
        {
            throw new InvalidOperationException($"Questionnaire field '{field.Code}' has minLength above maxLength.");
        }

        return new QuestionnaireFieldDefinition(
            field.Code,
            field.Name,
            field.Type,
            field.Required,
            field.MinLength,
            field.MaxLength,
            field.Format);
    }
}
