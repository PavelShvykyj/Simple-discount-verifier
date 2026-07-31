namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

public static class QuestionnaireValidationErrorCodes
{
    public const string AnswersRequired = "answers_required";
    public const string AnswerCodeRequired = "answer_code_required";
    public const string UnknownAnswerCode = "unknown_answer_code";
    public const string DuplicateAnswerCode = "duplicate_answer_code";
    public const string AnswerRequired = "answer_required";
    public const string InvalidAnswerFormat = "invalid_answer_format";

    public static string WithCode(string errorCode, string answerCode) => $"{errorCode}:{answerCode}";
}
