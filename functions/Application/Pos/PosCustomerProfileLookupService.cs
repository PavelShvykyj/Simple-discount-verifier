using System.Net;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Contracts.Admin;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;
using SimpleDiscountVerifier.Api.Domain.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Shared;

namespace SimpleDiscountVerifier.Api.Application.Pos;

public sealed class PosCustomerProfileLookupService
{
    private readonly ICustomerProfileRepository _profiles;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;

    public PosCustomerProfileLookupService(
        ICustomerProfileRepository profiles,
        IAuditWriter auditWriter,
        IClock clock)
    {
        _profiles = profiles;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    public async Task<ApplicationResult<PosCustomerProfileLookupResult>> LookupAsync(
        LookupPosCustomerProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!NormalizedPhoneNumber.TryCreate(command.Phone, out var phone))
        {
            return Failure(
                PosErrorCodes.InvalidRequest,
                "Phone is missing or invalid.",
                HttpStatusCode.BadRequest);
        }

        var correlationId = CorrelationIdGenerator.Create();
        await WriteAuditAsync(
            correlationId,
            PosProfileAuditEventTypes.PosProfileRequested,
            phone,
            command,
            cancellationToken);

        var profile = await _profiles.GetByPhoneAsync(phone, cancellationToken);

        if (profile is null)
        {
            await WriteAuditAsync(
                correlationId,
                PosProfileAuditEventTypes.PosProfileNotFound,
                phone,
                command,
                cancellationToken);

            return Failure(
                CustomerProfileErrorCodes.ProfileNotFound,
                "Profile was not found for this phone.",
                HttpStatusCode.NotFound);
        }

        await WriteAuditAsync(
            correlationId,
            PosProfileAuditEventTypes.PosProfileReturned,
            phone,
            command,
            cancellationToken);

        return ApplicationResult<PosCustomerProfileLookupResult>.Success(
            new PosCustomerProfileLookupResult(
                Found: true,
                ToCustomerProfileResponse(profile),
                _clock.UtcNow));
    }

    private async Task WriteAuditAsync(
        string correlationId,
        string eventType,
        NormalizedPhoneNumber phone,
        LookupPosCustomerProfileCommand command,
        CancellationToken cancellationToken)
    {
        await _auditWriter.WriteAsync(
            new AuditWriteRequest(
                correlationId,
                eventType,
                AuditActorTypes.Pos,
                command.Client.ClientId,
                phone,
                new Dictionary<string, string?>
                {
                    ["terminalId"] = command.TerminalId,
                    ["branchId"] = command.BranchId
                }),
            cancellationToken);
    }

    private static CustomerProfileResponse ToCustomerProfileResponse(CustomerProfileRecord profile) =>
        new(
            profile.Phone.Value,
            profile.Answers
                .Select(answer => new CustomerProfileAnswerDto(answer.Code, answer.Name, answer.Value))
                .ToArray(),
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);

    private static ApplicationResult<PosCustomerProfileLookupResult> Failure(
        string code,
        string message,
        HttpStatusCode statusCode) =>
        ApplicationResult<PosCustomerProfileLookupResult>.Failure(
            new ApplicationError(code, message, statusCode));
}
