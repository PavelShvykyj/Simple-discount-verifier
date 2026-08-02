namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public static class RedemptionErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidPhone = "invalid_phone";
    public const string TurnstileVerificationFailed = "turnstile_verification_failed";
    public const string SmsRetryTooSoon = "sms_retry_too_soon";
    public const string RedemptionNotFound = "redemption_not_found";
    public const string SmsExpired = "sms_expired";
    public const string InvalidSmsCode = "invalid_sms_code";
    public const string SmsAttemptsExceeded = "sms_attempts_exceeded";
    public const string RedemptionAlreadyCompleted = "redemption_already_completed";
    public const string RedemptionConflict = "redemption_conflict";
}
