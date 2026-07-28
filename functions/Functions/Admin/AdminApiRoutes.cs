namespace SimpleDiscountVerifier.Api.Functions.Admin;

public static class AdminApiRoutes
{
    public const string BackofficeCustomerProfiles = "backoffice/customer-profiles";
    public const string BackofficeCustomerProfileActivationSms =
        BackofficeCustomerProfiles + "/activation-code-sms";
    public const string BackofficeCustomerProfileByPhone = BackofficeCustomerProfiles + "/by-phone/{phone}";
    public const string BackofficeAuditEvents = "backoffice/audit-events";
    public const string BackofficeRedemptionsInspect = "backoffice/redemptions/inspect";
}
