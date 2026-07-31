namespace SimpleDiscountVerifier.Api.Contracts.System;

public sealed record CleanupResponse(
    DateTimeOffset RanAt,
    CleanupTableResponse DiscountRuntime,
    CleanupTableResponse AuditEvents);

public sealed record CleanupTableResponse(
    DateTimeOffset CutoffUtc,
    int Scanned,
    int Deleted,
    int Skipped,
    int Failed);
