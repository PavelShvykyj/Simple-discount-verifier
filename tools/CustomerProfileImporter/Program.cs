using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

var options = ImportOptions.Parse(args);

if (options is null)
{
    ImportOptions.PrintUsage();
    return 2;
}

var startedAtUtc = DateTimeOffset.UtcNow;
var rows = await ImportFileReader.ReadAsync(options.InputPath);
var mapping = await ImportMappingReader.ReadAsync(options.MappingPath);
var tableClient = options.DryRun
    ? null
    : new TableClient(options.ConnectionString, options.TableName);

if (tableClient is not null)
{
    await tableClient.CreateIfNotExistsAsync();
}

var summary = new ImportSummary(Total: rows.Count);
var skipReasons = new Dictionary<string, int>(StringComparer.Ordinal);
var validRows = new List<CustomerProfileImportRow>();

for (var index = 0; index < rows.Count; index++)
{
    var lineNumber = index + 1;
    var row = rows[index];
    var entityResult = CustomerProfileEntityFactory.TryCreate(row, mapping, startedAtUtc);

    if (!entityResult.Succeeded)
    {
        summary = summary with { Skipped = summary.Skipped + 1 };
        AddSkipReason(skipReasons, entityResult.Error);
        if (!options.Quiet)
        {
            Console.WriteLine($"SKIP row {lineNumber}: {entityResult.Error}");
        }

        continue;
    }

    if (options.DryRun)
    {
        summary = summary with { Valid = summary.Valid + 1 };
        if (!options.Quiet)
        {
            Console.WriteLine($"DRY-RUN row {lineNumber}: {entityResult.Phone}");
        }

        continue;
    }

    summary = summary with { Valid = summary.Valid + 1 };
    validRows.Add(new CustomerProfileImportRow(lineNumber, entityResult.Entity!, entityResult.Phone!));
}

if (!options.DryRun)
{
    summary = await CustomerProfileTableWriter.WriteBatchesAsync(
        tableClient!,
        validRows,
        options.Mode,
        options.Quiet,
        summary);
}

Console.WriteLine();
Console.WriteLine("Import summary");
Console.WriteLine($"  total: {summary.Total}");
Console.WriteLine($"  valid rows: {summary.Valid}");
Console.WriteLine($"  inserted: {summary.Inserted}");
Console.WriteLine($"  updated: {summary.Updated}");
Console.WriteLine($"  duplicates skipped: {summary.Duplicates}");
Console.WriteLine($"  skipped: {summary.Skipped}");

if (skipReasons.Count > 0)
{
    Console.WriteLine("  skip reasons:");
    foreach (var reason in skipReasons.OrderByDescending(item => item.Value))
    {
        Console.WriteLine($"    {reason.Key}: {reason.Value}");
    }
}

return 0;

static void AddSkipReason(Dictionary<string, int> skipReasons, string? error)
{
    var reason = error switch
    {
        null => "unknown",
        var value when value.StartsWith("phone ", StringComparison.Ordinal) => "phone cannot be normalized",
        var value when value.StartsWith("profile answers are invalid", StringComparison.Ordinal) => "profile answers invalid",
        var value when value.StartsWith("Value ", StringComparison.Ordinal) => "source value invalid",
        _ => error
    };

    skipReasons[reason] = skipReasons.TryGetValue(reason, out var count)
        ? count + 1
        : 1;
}

internal sealed record ImportSummary(
    int Total,
    int Valid = 0,
    int Inserted = 0,
    int Updated = 0,
    int Duplicates = 0,
    int Skipped = 0);

internal enum ImportMode
{
    Insert,
    Upsert,
    SkipExisting
}

internal enum ImportWriteStatus
{
    Inserted,
    Updated,
    DuplicateSkipped
}

internal sealed record CustomerProfileImportRow(
    int LineNumber,
    TableEntity Entity,
    string Phone);

internal sealed record ImportOptions(
    string InputPath,
    string MappingPath,
    string ConnectionString,
    string TableName,
    ImportMode Mode,
    bool DryRun,
    bool Quiet)
{
    public static ImportOptions? Parse(string[] args)
    {
        string? input = null;
        string? mapping = null;
        string? connectionString = null;
        var tableName = "CustomerProfiles";
        var mode = ImportMode.Insert;
        var dryRun = false;
        var quiet = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "--input":
                    input = ReadValue(args, ref index, arg);
                    break;
                case "--mapping":
                    mapping = ReadValue(args, ref index, arg);
                    break;
                case "--connection-string":
                    connectionString = ReadValue(args, ref index, arg);
                    break;
                case "--table-name":
                    tableName = ReadValue(args, ref index, arg) ?? tableName;
                    break;
                case "--mode":
                    var modeValue = ReadValue(args, ref index, arg);
                    if (!TryParseMode(modeValue, out mode))
                    {
                        Console.Error.WriteLine($"Unknown --mode value '{modeValue}'.");
                        return null;
                    }

                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{arg}'.");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.Error.WriteLine("--input is required.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(mapping))
        {
            Console.Error.WriteLine("--mapping is required.");
            return null;
        }

        connectionString ??= Environment.GetEnvironmentVariable("AppStorageConnectionString");

        if (!dryRun && string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("--connection-string is required unless --dry-run is used.");
            return null;
        }

        return new ImportOptions(input, mapping, connectionString ?? string.Empty, tableName, mode, dryRun, quiet);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Usage:
              dotnet run --project tools/CustomerProfileImporter -- --input <file.json> --mapping <mapping.json> [--connection-string <storage-connection-string>] [--table-name CustomerProfiles] [--mode insert|upsert|skip-existing] [--dry-run] [--quiet]

            Input JSON:
              { "ancets": [ { "tel": "+38 (050) 123-45-67", "fio": "ФИО", "dish": "блюдо" } ] }

            Mapping JSON:
              { "fields": { "tel": "phone", "fio": "fullName", "dish": "favoriteDish" }, "dateFormats": { "birthDate": [ "yyyy-MM-dd", "dd.MM.yyyy H:mm:ss" ] } }

            Notes:
              --mode insert        Adds new rows and reports duplicates as skipped.
              --mode upsert        Inserts new rows or replaces existing rows.
              --mode skip-existing Skips rows that already exist by normalized phone.
              --dry-run            Validates and normalizes without writing to Azure Table Storage.
              --quiet              Prints only the final summary.
            """);
    }

    private static string? ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine($"{name} requires a value.");
            return null;
        }

        index++;
        return args[index];
    }

    private static bool TryParseMode(string? value, out ImportMode mode)
    {
        mode = ImportMode.Insert;

        return value switch
        {
            "insert" => true,
            "upsert" => SetMode(ImportMode.Upsert, out mode),
            "skip-existing" => SetMode(ImportMode.SkipExisting, out mode),
            _ => false
        };
    }

    private static bool SetMode(ImportMode value, out ImportMode mode)
    {
        mode = value;
        return true;
    }
}

internal static class ImportFileReader
{
    public static async Task<IReadOnlyList<Dictionary<string, string?>>> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ImportDocument>(stream, JsonOptions.Default);

        if (document?.Ancets is null)
        {
            throw new InvalidOperationException("Input JSON must contain an 'ancets' array.");
        }

        return document.Ancets;
    }
}

internal sealed class ImportDocument
{
    [JsonPropertyName("ancets")]
    public IReadOnlyList<Dictionary<string, string?>>? Ancets { get; init; }
}

internal sealed record ImportMapping(
    string PhoneSourceKey,
    IReadOnlyDictionary<string, string> AnswerSourceKeysByCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>> DateFormatsByCode);

internal static class ImportMappingReader
{
    private const string PhoneTargetKey = "phone";

    public static async Task<ImportMapping> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ImportMappingDocument>(stream, JsonOptions.Default);

        if (document?.Fields is null || document.Fields.Count == 0)
        {
            throw new InvalidOperationException("Mapping JSON must contain a non-empty 'fields' object.");
        }

        string? phoneSourceKey = null;
        var answerSourceKeysByCode = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (sourceKey, targetKey) in document.Fields)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                throw new InvalidOperationException("Mapping source keys must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new InvalidOperationException($"Mapping target for source key '{sourceKey}' must not be empty.");
            }

            var normalizedTargetKey = targetKey.Trim();

            if (normalizedTargetKey == PhoneTargetKey)
            {
                if (phoneSourceKey is not null)
                {
                    throw new InvalidOperationException("Mapping must contain exactly one source key mapped to 'phone'.");
                }

                phoneSourceKey = sourceKey;
                continue;
            }

            if (!answerSourceKeysByCode.TryAdd(normalizedTargetKey, sourceKey))
            {
                throw new InvalidOperationException(
                    $"Questionnaire answer '{normalizedTargetKey}' is mapped more than once.");
            }
        }

        if (phoneSourceKey is null)
        {
            throw new InvalidOperationException("Mapping must contain one source key mapped to 'phone'.");
        }

        var dateFormatsByCode = document.DateFormats?.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value,
            StringComparer.Ordinal) ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        return new ImportMapping(phoneSourceKey, answerSourceKeysByCode, dateFormatsByCode);
    }
}

internal sealed class ImportMappingDocument
{
    [JsonPropertyName("fields")]
    public Dictionary<string, string>? Fields { get; init; }

    [JsonPropertyName("dateFormats")]
    public Dictionary<string, string[]>? DateFormats { get; init; }
}

internal sealed record CustomerProfileEntityResult(
    bool Succeeded,
    TableEntity? Entity,
    string? Phone,
    string? Error)
{
    public static CustomerProfileEntityResult Success(TableEntity entity, string phone) =>
        new(true, entity, phone, null);

    public static CustomerProfileEntityResult Failure(string error) =>
        new(false, null, null, error);
}

internal static class CustomerProfileEntityFactory
{
    public static CustomerProfileEntityResult TryCreate(
        IReadOnlyDictionary<string, string?> row,
        ImportMapping mapping,
        DateTimeOffset importedAtUtc)
    {
        if (!row.TryGetValue(mapping.PhoneSourceKey, out var rawPhone)
            || !NormalizedPhoneNumber.TryCreate(rawPhone, out var phone))
        {
            return CustomerProfileEntityResult.Failure($"phone '{rawPhone}' cannot be normalized");
        }

        IReadOnlyList<QuestionnaireInputAnswer> inputAnswers;

        try
        {
            inputAnswers = mapping.AnswerSourceKeysByCode.Select(mappingItem =>
            {
                row.TryGetValue(mappingItem.Value, out var value);
                var normalizedValue = SourceValueNormalizer.Normalize(mappingItem.Key, value, mapping);
                return new QuestionnaireInputAnswer(mappingItem.Key, normalizedValue);
            }).ToArray();
        }
        catch (InvalidOperationException exception)
        {
            return CustomerProfileEntityResult.Failure(exception.Message);
        }

        var answersResult = QuestionnaireDefinition.Validate(inputAnswers);

        if (!answersResult.IsValid || answersResult.Value is null)
        {
            return CustomerProfileEntityResult.Failure(
                $"profile answers are invalid for phone {phone.Value}: {string.Join(", ", answersResult.Errors)}");
        }

        var entity = new TableEntity("phone", phone.StorageKey)
        {
            ["Phone"] = phone.Value,
            ["AnswersJson"] = JsonSerializer.Serialize(answersResult.Value, JsonOptions.Default),
            ["CreatedAtUtc"] = importedAtUtc,
            ["UpdatedAtUtc"] = importedAtUtc
        };

        return CustomerProfileEntityResult.Success(entity, phone.Value);
    }
}

internal static class SourceValueNormalizer
{
    private const string OutputDateFormat = "yyyy-MM-dd";

    public static string? Normalize(string answerCode, string? value, ImportMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return mapping.DateFormatsByCode.TryGetValue(answerCode, out var dateFormats)
            ? NormalizeDate(answerCode, trimmed, dateFormats)
            : trimmed;
    }

    private static string NormalizeDate(
        string answerCode,
        string value,
        IReadOnlyList<string> dateFormats)
    {
        if (dateFormats.Count == 0)
        {
            return value;
        }

        foreach (var format in dateFormats)
        {
            if (DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
            {
                return dateTime.ToString(OutputDateFormat, CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException(
            $"Value '{value}' for answer '{answerCode}' does not match configured date formats.");
    }
}

internal static class CustomerProfileTableWriter
{
    private const int BatchSize = 100;

    public static async Task<ImportSummary> WriteBatchesAsync(
        TableClient tableClient,
        IReadOnlyList<CustomerProfileImportRow> rows,
        ImportMode mode,
        bool quiet,
        ImportSummary summary)
    {
        foreach (var batch in rows.Chunk(BatchSize))
        {
            var batchSummary = await WriteBatchAsync(tableClient, batch, mode, quiet);
            summary = summary with
            {
                Inserted = summary.Inserted + batchSummary.Inserted,
                Updated = summary.Updated + batchSummary.Updated,
                Duplicates = summary.Duplicates + batchSummary.Duplicates
            };
        }

        return summary;
    }

    private static async Task<ImportSummary> WriteBatchAsync(
        TableClient tableClient,
        CustomerProfileImportRow[] batch,
        ImportMode mode,
        bool quiet)
    {
        try
        {
            var actions = batch
                .Select(row => new TableTransactionAction(ToTransactionActionType(mode), row.Entity))
                .ToArray();

            await tableClient.SubmitTransactionAsync(actions);

            if (!quiet)
            {
                Console.WriteLine(
                    $"BATCH {mode.ToString().ToUpperInvariant()} rows {batch[0].LineNumber}-{batch[^1].LineNumber}: {batch.Length}");
            }

            return mode == ImportMode.Upsert
                ? new ImportSummary(Total: 0, Updated: batch.Length)
                : new ImportSummary(Total: 0, Inserted: batch.Length);
        }
        catch (RequestFailedException exception)
        {
            if (!quiet)
            {
                Console.WriteLine(
                    $"BATCH FALLBACK rows {batch[0].LineNumber}-{batch[^1].LineNumber}: {exception.Status} {exception.ErrorCode}");
            }

            return await WriteRowsIndividuallyAsync(tableClient, batch, mode, quiet);
        }
    }

    private static async Task<ImportSummary> WriteRowsIndividuallyAsync(
        TableClient tableClient,
        IReadOnlyList<CustomerProfileImportRow> rows,
        ImportMode mode,
        bool quiet)
    {
        var summary = new ImportSummary(Total: 0);

        foreach (var row in rows)
        {
            var status = await WriteAsync(tableClient, row.Entity, mode);

            summary = status switch
            {
                ImportWriteStatus.Inserted => summary with { Inserted = summary.Inserted + 1 },
                ImportWriteStatus.Updated => summary with { Updated = summary.Updated + 1 },
                ImportWriteStatus.DuplicateSkipped => summary with { Duplicates = summary.Duplicates + 1 },
                _ => summary
            };

            if (!quiet)
            {
                Console.WriteLine($"{status.ToString().ToUpperInvariant()} row {row.LineNumber}: {row.Phone}");
            }
        }

        return summary;
    }

    public static async Task<ImportWriteStatus> WriteAsync(
        TableClient tableClient,
        TableEntity entity,
        ImportMode mode)
    {
        if (mode == ImportMode.Upsert)
        {
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return ImportWriteStatus.Updated;
        }

        try
        {
            await tableClient.AddEntityAsync(entity);
            return ImportWriteStatus.Inserted;
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            if (mode == ImportMode.SkipExisting || mode == ImportMode.Insert)
            {
                return ImportWriteStatus.DuplicateSkipped;
            }

            throw;
        }
    }

    private static TableTransactionActionType ToTransactionActionType(ImportMode mode)
    {
        return mode == ImportMode.Upsert
            ? TableTransactionActionType.UpsertReplace
            : TableTransactionActionType.Add;
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
