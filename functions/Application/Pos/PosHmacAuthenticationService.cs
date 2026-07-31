using System.Net;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Domain.Security;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Application.Pos;

public sealed class PosHmacAuthenticationService
{
    private const string PosMainClientIdSettingName = "PosMainClientId";
    private const string PosMainClientHmacSecretSettingName = "PosMainClientHmacSecret";
    private const string PosRequestFreshnessToleranceSecondsSettingName = "PosRequestFreshnessToleranceSeconds";

    private readonly PosOptions _options;
    private readonly IClock _clock;

    public PosHmacAuthenticationService(
        IOptions<PosOptions> options,
        IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public ApplicationResult<PosClientIdentity> Authenticate(PosAuthenticateCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ClientId)
            || string.IsNullOrWhiteSpace(command.Timestamp)
            || string.IsNullOrWhiteSpace(command.Signature))
        {
            return Unauthorized();
        }

        var validation = PosHmacValidator.Validate(
            new PosHmacValidationRequest(
                command.Method,
                command.Path,
                command.Timestamp,
                command.BodySha256Hex,
                command.ClientId,
                command.Signature),
            new PosHmacValidationOptions(
                RequireSetting(_options.MainClientId, PosMainClientIdSettingName),
                RequireSetting(_options.MainClientHmacSecret, PosMainClientHmacSecretSettingName),
                RequirePositiveSeconds(
                    _options.RequestFreshnessToleranceSeconds,
                    PosRequestFreshnessToleranceSecondsSettingName)),
            _clock.UtcNow);

        return validation.IsValid
            ? ApplicationResult<PosClientIdentity>.Success(new PosClientIdentity(command.ClientId))
            : Unauthorized();
    }

    private static ApplicationResult<PosClientIdentity> Unauthorized() =>
        ApplicationResult<PosClientIdentity>.Failure(
            new ApplicationError(
                PosErrorCodes.Unauthorized,
                "POS authentication failed.",
                HttpStatusCode.Unauthorized));

    private static string RequireSetting(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Application setting '{settingName}' is required.");
        }

        return value;
    }

    private static int RequirePositiveSeconds(int value, string settingName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"Application setting '{settingName}' must be a positive integer.");
        }

        return value;
    }
}
