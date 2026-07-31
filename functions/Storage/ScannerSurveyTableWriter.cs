using System.Text.RegularExpressions;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Infrastructure.Options;
using SimpleDiscountVerifier.Api.Models;

namespace SimpleDiscountVerifier.Api.Storage;

public sealed partial class ScannerSurveyTableWriter : IScannerSurveyTableWriter
{
    private const string StorageConnectionSettingName = "AppStorageConnectionString";
    private const string TableNameSettingName = "ScannerSurveyTableName";

    private readonly TableClient _tableClient;

    public ScannerSurveyTableWriter(IOptions<StorageOptions> options)
    {
        var storageOptions = options.Value;

        if (string.IsNullOrWhiteSpace(storageOptions.AppStorageConnectionString))
        {
            throw new InvalidOperationException(
                $"Application setting '{StorageConnectionSettingName}' is required.");
        }

        if (string.IsNullOrWhiteSpace(storageOptions.ScannerSurveyTableName))
        {
            throw new InvalidOperationException(
                $"Application setting '{TableNameSettingName}' is required.");
        }

        _tableClient = new TableClient(
            storageOptions.AppStorageConnectionString,
            storageOptions.ScannerSurveyTableName);
    }

    public async Task<int> WriteAsync(
        ScannerSurveyRequest request,
        string submissionId,
        DateTimeOffset submittedAtUtc,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var branchKey = BuildBranchKey(request.BranchName);
        var entities = BuildEntities(request, branchKey, submissionId, submittedAtUtc, userAgent);
        var rowsWritten = 0;

        foreach (var entity in entities)
        {
            await _tableClient.AddEntityAsync(entity, cancellationToken);
            rowsWritten++;
        }

        return rowsWritten;
    }

    private static IEnumerable<TableEntity> BuildEntities(
        ScannerSurveyRequest request,
        string branchKey,
        string submissionId,
        DateTimeOffset submittedAtUtc,
        string? userAgent)
    {
        var terminals = request.Terminals ?? [];
        var wroteAnyEntity = false;

        for (var terminalIndex = 0; terminalIndex < terminals.Count; terminalIndex++)
        {
            var terminal = terminals[terminalIndex];
            var answers = terminal.Answers ?? [];

            if (answers.Count == 0)
            {
                wroteAnyEntity = true;
                yield return CreateEntity(
                    request,
                    branchKey,
                    submissionId,
                    submittedAtUtc,
                    userAgent,
                    "terminal",
                    terminal.TerminalName,
                    terminalIndex,
                    null,
                    null,
                    null,
                    null);

                continue;
            }

            for (var answerIndex = 0; answerIndex < answers.Count; answerIndex++)
            {
                var answer = answers[answerIndex];
                wroteAnyEntity = true;

                yield return CreateEntity(
                    request,
                    branchKey,
                    submissionId,
                    submittedAtUtc,
                    userAgent,
                    "answer",
                    terminal.TerminalName,
                    terminalIndex,
                    answerIndex,
                    answer.BarcodeId,
                    answer.IsReadable,
                    answer.Comment);
            }
        }

        if (!wroteAnyEntity)
        {
            yield return CreateEntity(
                request,
                branchKey,
                submissionId,
                submittedAtUtc,
                userAgent,
                "submission",
                null,
                null,
                null,
                null,
                null,
                null);
        }
    }

    private static TableEntity CreateEntity(
        ScannerSurveyRequest request,
        string branchKey,
        string submissionId,
        DateTimeOffset submittedAtUtc,
        string? userAgent,
        string recordType,
        string? terminalName,
        int? terminalIndex,
        int? answerIndex,
        string? barcodeId,
        bool? isReadable,
        string? answerComment)
    {
        var rowKey = $"{submittedAtUtc.UtcTicks:D20}-{terminalIndex ?? -1:D3}-{answerIndex ?? -1:D3}-{Guid.NewGuid():N}";

        return new TableEntity(branchKey, rowKey)
        {
            ["RecordType"] = recordType,
            ["SubmissionId"] = submissionId,
            ["BranchName"] = request.BranchName,
            ["TerminalName"] = terminalName,
            ["TerminalIndex"] = terminalIndex,
            ["AnswerIndex"] = answerIndex,
            ["BarcodeId"] = barcodeId,
            ["IsReadable"] = isReadable,
            ["AnswerComment"] = answerComment,
            ["SubmissionComment"] = request.Comment,
            ["SubmittedAtUtc"] = submittedAtUtc,
            ["SubmittedAtClient"] = request.SubmittedAtClient,
            ["UserAgent"] = userAgent
        };
    }

    private static string BuildBranchKey(string? branchName)
    {
        var normalized = string.IsNullOrWhiteSpace(branchName)
            ? "empty-branch"
            : InvalidTableKeyCharacters().Replace(branchName.Trim(), "-");

        normalized = Regex.Replace(normalized, @"\s+", "-").ToLowerInvariant();
        normalized = normalized.Length == 0 ? "empty-branch" : normalized;

        return normalized.Length <= 128 ? normalized : normalized[..128];
    }

    [GeneratedRegex(@"[\/\\#?]")]
    private static partial Regex InvalidTableKeyCharacters();
}
