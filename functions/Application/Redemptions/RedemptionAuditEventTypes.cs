namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public static class RedemptionAuditEventTypes
{
    public const string RedemptionStarted = "redemption_started";
    public const string InvalidPhone = "invalid_phone";
    public const string ProfileNotFound = "profile_not_found";
    public const string ProfileFound = "profile_found";
    public const string SmsRetryTooSoon = "sms_retry_too_soon";
    public const string SmsSent = "sms_sent";
    public const string SmsSendFailed = "sms_send_failed";
    public const string SmsValidationFailed = "sms_validation_failed";
    public const string SmsExpired = "sms_expired";
    public const string SmsAttemptsExceeded = "sms_attempts_exceeded";
    public const string PhoneVerified = "phone_verified";
    public const string BarcodeIssued = "barcode_issued";
}
