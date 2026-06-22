using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Application.Sms;

public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken);
}

public sealed record SmsSendRequest(
    NormalizedPhoneNumber Phone,
    string Message);

public sealed record SmsSendResult(
    bool Accepted,
    string? ProviderMessageId = null,
    string? FailureReason = null);
