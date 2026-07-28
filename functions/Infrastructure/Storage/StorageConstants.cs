namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

internal static class StorageConstants
{
    public const string CustomerProfilePartitionKey = "phone";
    public const string RuntimePartitionKeyPrefix = "phone:";
    public const string CurrentRuntimeRowKey = "current";

    public const int AuditRowKeyRandomSuffixLength = 8;
    public const int AuditInsertMaxAttempts = 3;

    public static class Properties
    {
        public const string Phone = "Phone";
        public const string PhysicalCardNumber = "PhysicalCardNumber";
        public const string AnswersJson = "AnswersJson";
        public const string CreatedAtUtc = "CreatedAtUtc";
        public const string UpdatedAtUtc = "UpdatedAtUtc";
        public const string CorrelationId = "CorrelationId";
        public const string SmsCodeHash = "SmsCodeHash";
        public const string SmsAttempts = "SmsAttempts";
        public const string SmsMaxAttempts = "SmsMaxAttempts";
        public const string SmsSentAtUtc = "SmsSentAtUtc";
        public const string SmsExpiresAtUtc = "SmsExpiresAtUtc";
        public const string PhoneVerifiedAtUtc = "PhoneVerifiedAtUtc";
        public const string BarcodeHash = "BarcodeHash";
        public const string BarcodeExpiresAtUtc = "BarcodeExpiresAtUtc";
        public const string BarcodeConsumedAtUtc = "BarcodeConsumedAtUtc";
        public const string ConsumedByScanId = "ConsumedByScanId";
        public const string EventType = "EventType";
        public const string PhoneHash = "PhoneHash";
        public const string ActorType = "ActorType";
        public const string ActorId = "ActorId";
        public const string OccurredAtUtc = "OccurredAtUtc";
        public const string MetadataJson = "MetadataJson";
    }
}
