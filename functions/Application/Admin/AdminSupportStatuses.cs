namespace SimpleDiscountVerifier.Api.Application.Admin;

public static class AdminSupportStatuses
{
    public const string Started = "started";
    public const string ProfileNotFound = "profile_not_found";
    public const string SmsSendFailed = "sms_send_failed";
    public const string SmsSent = "sms_sent";
    public const string SmsFailed = "sms_failed";
    public const string BarcodeIssued = "barcode_issued";
    public const string BarcodeConsumed = "barcode_consumed";
    public const string BarcodeExpired = "barcode_expired";
    public const string Unknown = "unknown";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Consumed = "consumed";
    public const string ReplacedByNewFlow = "replaced_by_new_flow";
    public const string InvalidFormat = "invalid_format";
    public const string NotIssued = "not_issued";
}
