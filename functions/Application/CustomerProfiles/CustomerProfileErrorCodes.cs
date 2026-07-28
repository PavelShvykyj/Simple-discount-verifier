namespace SimpleDiscountVerifier.Api.Application.CustomerProfiles;

public static class CustomerProfileErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidPhone = "invalid_phone";
    public const string InvalidPhysicalCardNumber = "invalid_physical_card_number";
    public const string InvalidProfileAnswers = "invalid_profile_answers";
    public const string DuplicateProfile = "duplicate_profile";
    public const string ProfileNotFound = "profile_not_found";
}
