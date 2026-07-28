using System.Net;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Contracts.Admin;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;
using SimpleDiscountVerifier.Api.Domain.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Shared;

namespace SimpleDiscountVerifier.Api.Application.CustomerProfiles;

public sealed class AdminCustomerProfileService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private sealed record ValidatedProfile(
        NormalizedPhoneNumber Phone,
        PhysicalCardNumber PhysicalCardNumber,
        IReadOnlyList<QuestionnaireAnswer> Answers);

    private readonly ICustomerProfileRepository _profiles;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;

    public AdminCustomerProfileService(
        ICustomerProfileRepository profiles,
        IAuditWriter auditWriter,
        IClock clock)
    {
        _profiles = profiles;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    public async Task<ApplicationResult<CustomerProfileResponse>> CreateAsync(
        CustomerProfileRequest? request,
        string? adminActorId,
        CancellationToken cancellationToken)
    {
        var validation = ValidateProfileRequest(request);

        if (!validation.IsSuccess)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(validation.Error!);
        }

        var (phone, physicalCardNumber, answers) = validation.Value!;
        var now = _clock.UtcNow;
        var profile = new CustomerProfileRecord(phone, physicalCardNumber, answers, now, now);
        var writeResult = await _profiles.InsertAsync(profile, cancellationToken);

        if (writeResult.Status == StorageWriteStatus.Conflict)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(DuplicateProfile());
        }

        await WriteProfileAuditAsync(
            CustomerProfileAuditEventTypes.CustomerProfileCreated,
            phone,
            adminActorId,
            cancellationToken);

        return ApplicationResult<CustomerProfileResponse>.Success(ToResponse(profile));
    }

    public async Task<ApplicationResult<AdminCustomerProfileListResult>> ListAsync(
        AdminCustomerProfileListQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = NormalizePageSize(query.PageSize);

        if (!string.IsNullOrWhiteSpace(query.Phone))
        {
            if (!NormalizedPhoneNumber.TryCreate(query.Phone, out var phone))
            {
                return ApplicationResult<AdminCustomerProfileListResult>.Failure(InvalidPhone());
            }

            var profile = await _profiles.GetByPhoneAsync(phone, cancellationToken);
            var items = profile is null
                ? []
                : new[] { ToResponse(profile) };

            return ApplicationResult<AdminCustomerProfileListResult>.Success(new(items, null));
        }

        var page = await _profiles.ListAsync(pageSize, query.ContinuationToken, cancellationToken);
        return ApplicationResult<AdminCustomerProfileListResult>.Success(
            new AdminCustomerProfileListResult(
                page.Items.Select(ToResponse).ToArray(),
                page.ContinuationToken));
    }

    public async Task<ApplicationResult<CustomerProfileResponse>> GetByPhoneAsync(
        string? phoneValue,
        CancellationToken cancellationToken)
    {
        if (!NormalizedPhoneNumber.TryCreate(phoneValue, out var phone))
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(InvalidPhone());
        }

        var profile = await _profiles.GetByPhoneAsync(phone, cancellationToken);

        return profile is null
            ? ApplicationResult<CustomerProfileResponse>.Failure(ProfileNotFound())
            : ApplicationResult<CustomerProfileResponse>.Success(ToResponse(profile));
    }

    public async Task<ApplicationResult<CustomerProfileResponse>> UpdateAsync(
        string? pathPhoneValue,
        CustomerProfileRequest? request,
        string? adminActorId,
        CancellationToken cancellationToken)
    {
        if (!NormalizedPhoneNumber.TryCreate(pathPhoneValue, out var pathPhone))
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(InvalidPhone());
        }

        var existing = await _profiles.GetByPhoneAsync(pathPhone, cancellationToken);

        if (existing is null)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(ProfileNotFound());
        }

        if (string.IsNullOrWhiteSpace(existing.ConcurrencyToken))
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(InvalidRequest("Stored profile concurrency token is missing."));
        }

        var validation = ValidateProfileRequest(request);

        if (!validation.IsSuccess)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(validation.Error!);
        }

        var (requestPhone, physicalCardNumber, answers) = validation.Value!;
        var updated = existing with
        {
            Phone = requestPhone,
            PhysicalCardNumber = physicalCardNumber,
            Answers = answers,
            UpdatedAtUtc = _clock.UtcNow
        };

        var writeResult = requestPhone == pathPhone
            ? await _profiles.ReplaceAsync(updated, existing.ConcurrencyToken, cancellationToken)
            : await _profiles.ChangePhoneAsync(pathPhone, existing.ConcurrencyToken, updated, cancellationToken);

        if (writeResult.Status == StorageWriteStatus.Conflict)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(DuplicateProfile());
        }

        if (writeResult.Status == StorageWriteStatus.NotFound)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(ProfileNotFound());
        }

        if (writeResult.Status == StorageWriteStatus.PreconditionFailed)
        {
            return ApplicationResult<CustomerProfileResponse>.Failure(Conflict("Profile was changed by another request."));
        }

        await WriteProfileAuditAsync(
            CustomerProfileAuditEventTypes.CustomerProfileUpdated,
            requestPhone,
            adminActorId,
            cancellationToken);

        return ApplicationResult<CustomerProfileResponse>.Success(ToResponse(updated));
    }

    private static ApplicationResult<ValidatedProfile> ValidateProfileRequest(
        CustomerProfileRequest? request)
    {
        if (request is null)
        {
            return ApplicationResult<ValidatedProfile>.Failure(
                InvalidRequest("Request body is required."));
        }

        if (!NormalizedPhoneNumber.TryCreate(request.Phone, out var phone))
        {
            return ApplicationResult<ValidatedProfile>.Failure(InvalidPhone());
        }

        if (!PhysicalCardNumber.TryCreate(request.PhysicalCardNumber, out var physicalCardNumber))
        {
            return ApplicationResult<ValidatedProfile>.Failure(InvalidPhysicalCardNumber());
        }

        var answers = request.Answers?.Select(answer => new QuestionnaireInputAnswer(answer.Code, answer.Value));
        var answersResult = QuestionnaireDefinition.Validate(answers);

        if (!answersResult.IsValid)
        {
            return ApplicationResult<ValidatedProfile>.Failure(
                InvalidProfileAnswers());
        }

        return ApplicationResult<ValidatedProfile>.Success(
            new ValidatedProfile(phone, physicalCardNumber, answersResult.Value!));
    }

    private static int NormalizePageSize(int? pageSize)
    {
        if (pageSize is null or <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(pageSize.Value, MaxPageSize);
    }

    private async Task WriteProfileAuditAsync(
        string eventType,
        NormalizedPhoneNumber phone,
        string? adminActorId,
        CancellationToken cancellationToken)
    {
        await _auditWriter.WriteAsync(
            new AuditWriteRequest(
                CorrelationIdGenerator.Create(),
                eventType,
                AuditActorTypes.Admin,
                adminActorId,
                phone),
            cancellationToken);
    }

    private static CustomerProfileResponse ToResponse(CustomerProfileRecord profile)
    {
        return new CustomerProfileResponse(
            profile.Phone.Value,
            profile.PhysicalCardNumber.Value,
            profile.Answers
                .Select(answer => new CustomerProfileAnswerDto(answer.Code, answer.Name, answer.Value))
                .ToArray(),
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
    }

    private static ApplicationError InvalidRequest(string message) =>
        new(CustomerProfileErrorCodes.InvalidRequest, message, HttpStatusCode.BadRequest);

    private static ApplicationError InvalidPhone() =>
        new(CustomerProfileErrorCodes.InvalidPhone, "Phone is missing or invalid.", HttpStatusCode.BadRequest);

    private static ApplicationError InvalidPhysicalCardNumber() =>
        new(
            CustomerProfileErrorCodes.InvalidPhysicalCardNumber,
            "Physical card number is missing or invalid.",
            HttpStatusCode.BadRequest);

    private static ApplicationError InvalidProfileAnswers() =>
        new(CustomerProfileErrorCodes.InvalidProfileAnswers, "Profile answers are invalid.", HttpStatusCode.BadRequest);

    private static ApplicationError DuplicateProfile() =>
        new(CustomerProfileErrorCodes.DuplicateProfile, "A profile already exists for this phone.", HttpStatusCode.Conflict);

    private static ApplicationError ProfileNotFound() =>
        new(CustomerProfileErrorCodes.ProfileNotFound, "Profile was not found for this phone.", HttpStatusCode.NotFound);

    private static ApplicationError Conflict(string message) =>
        new(CustomerProfileErrorCodes.DuplicateProfile, message, HttpStatusCode.Conflict);
}
