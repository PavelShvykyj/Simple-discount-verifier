namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class HashingOptions
{
    public string PhoneRuntimeKeySecret { get; set; } = string.Empty;

    public string SmsCodeHashSecret { get; set; } = string.Empty;

    public string BarcodeHashSecret { get; set; } = string.Empty;

    public string AuditPhoneHashSecret { get; set; } = string.Empty;
}
