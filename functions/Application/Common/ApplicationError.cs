using System.Net;

namespace SimpleDiscountVerifier.Api.Application.Common;

public sealed record ApplicationError(
    string Code,
    string Message,
    HttpStatusCode StatusCode,
    string? CorrelationId = null);
