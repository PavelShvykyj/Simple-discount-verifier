namespace SimpleDiscountVerifier.Api.Application.System;

public sealed record SystemCleanupResult(
    DateTimeOffset RanAt,
    CleanupTableResult DiscountRuntime,
    CleanupTableResult AuditEvents);

public sealed record CleanupTableResult(
    DateTimeOffset CutoffUtc,
    int Scanned,
    int Deleted,
    int Skipped,
    int Failed);
